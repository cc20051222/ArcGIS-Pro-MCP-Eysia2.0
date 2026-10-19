"""Create a D-drive offline validation receipt. Never invokes MCP or GIS."""
import argparse
import hashlib
import importlib.util
import io
import json
import os
import re
import subprocess
import sys
import unittest
from pathlib import Path
from urllib.parse import unquote

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
PACKAGE = HERE.parent
SKILL = PACKAGE / 'arcgis-pro-2-workflows'


def validate_receipt_path(output, allowed_root):
    output, allowed_root = output.resolve(), allowed_root.resolve()
    if output.drive.upper() != 'D:' or allowed_root.drive.upper() != 'D:':
        raise ValueError('Receipt and explicitly allowed root must be on D drive.')
    if not output.is_relative_to(allowed_root) or output == allowed_root:
        raise ValueError('Receipt must stay within the explicitly allowed output root.')
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--format-validator', type=Path, required=True,
                        help='Existing skill-creator quick_validate.py. Read/execute only; no installation.')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--output-root', type=Path, default=PACKAGE,
                        help='Explicit D-drive owned receipt root; defaults to this Skills package.')
    parser.add_argument('--replace', action='store_true')
    args = parser.parse_args()
    try:
        output = validate_receipt_path(args.output, args.output_root)
    except ValueError as exc:
        parser.error(str(exc))
    if output.exists() and not args.replace:
        parser.error('Receipt exists; use --replace only for an intended candidate refresh.')
    import yaml  # Already present in the authoring environment; not needed for normal Skill use.
    skill_body = (SKILL / 'SKILL.md').read_text(encoding='utf-8')
    skill_meta = yaml.safe_load(skill_body.split('---', 2)[1])
    case_design = json.loads((HERE / 'behavior-cases.json').read_text(encoding='utf-8'))
    case_count = len(case_design['cases'])
    checks = []

    def check(name, action):
        try:
            detail = action()
            checks.append({'name': name, 'result': 'PASS', 'detail': detail})
        except Exception as exc:
            checks.append({'name': name, 'result': 'FAIL', 'detail': str(exc)})

    env = dict(os.environ, PYTHONUTF8='1', PYTHONDONTWRITEBYTECODE='1')

    def format_check():
        result = subprocess.run([sys.executable, '-B', str(args.format_validator), str(SKILL)],
                                capture_output=True, text=True, encoding='utf-8', env=env, check=False)
        if result.returncode:
            raise ValueError(result.stderr or result.stdout)
        return {'exitCode': result.returncode, 'stdout': result.stdout.strip()}
    check('bundled_skill_creator_format_validator', format_check)

    def metadata_check():
        body = (SKILL / 'SKILL.md').read_text(encoding='utf-8')
        meta = yaml.safe_load(body.split('---', 2)[1])
        ui = yaml.safe_load((SKILL / 'agents/openai.yaml').read_text(encoding='utf-8'))
        assert meta['name'] == SKILL.name
        assert len(meta['description']) <= 1024
        assert len(body.splitlines()) < 500
        assert 25 <= len(ui['interface']['short_description']) <= 64
        assert '$' + SKILL.name in ui['interface']['default_prompt']
        assert ui['policy']['allow_implicit_invocation'] is True
        assert 'allowed-tools' not in meta
        return {'mainLines': len(body.splitlines()), 'descriptionChars': len(meta['description']),
                'uiShortDescriptionChars': len(ui['interface']['short_description'])}
    check('entrypoint_and_ui_metadata', metadata_check)

    def link_check():
        count = 0
        for file in PACKAGE.rglob('*.md'):
            for target in re.findall(r'\]\(([^)]+)\)', file.read_text(encoding='utf-8')):
                if '://' in target or target.startswith(('#', 'mailto:')):
                    continue
                resolved = (file.parent / unquote(target.split('#')[0])).resolve()
                assert resolved == output or resolved.exists(), str(file) + ': ' + target
                count += 1
        return {'localLinksResolved': count}
    check('local_document_links', link_check)

    def json_check():
        files = [p for p in PACKAGE.rglob('*.json') if p.resolve() != output]
        for file in files:
            json.loads(file.read_text(encoding='utf-8-sig'))
        cases = json.loads((HERE / 'behavior-cases.json').read_text(encoding='utf-8'))
        ids = [case['id'] for case in cases['cases']]
        assert len(ids) == len(set(ids)) == cases['expectedCaseCount']
        assert cases['version'] == skill_meta['metadata']['version']
        task = json.loads((SKILL / 'assets/task-record.json').read_text(encoding='utf-8'))
        assert task['execution']['status'] == 'NOT_STARTED'
        assert task['schemaVersion'] == '1.3'
        assert task['authorization']['userMessageReference'] is None
        research = json.loads((HERE / 'research-sources.json').read_text(encoding='utf-8'))
        return {'jsonFilesParsed': len(files), 'behaviorCasesDefined': len(ids),
                'researchSourcesRecorded': len(research['sources'])}
    check('json_templates_research_and_cases', json_check)

    def tool_reference_check():
        catalog = json.loads((SKILL / 'references/tool-catalog.snapshot.json').read_text(encoding='utf-8'))
        known = {row['name'] for row in catalog['tools']}
        used = set()
        for file in [SKILL / 'SKILL.md', *SKILL.glob('references/*.md')]:
            for token in re.findall(r'`([a-z][a-z0-9_]+)`', file.read_text(encoding='utf-8')):
                if '_' in token:
                    assert token in known, file.name + ': ' + token
                    used.add(token)
        return {'exactToolNamesReferenced': len(used), 'unknownExactToolNames': []}
    check('documented_exact_tool_names_exist_in_source', tool_reference_check)

    spec = importlib.util.spec_from_file_location('offline_skill_tests', HERE / 'test_skill_helpers.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    stream = io.StringIO()
    suite = unittest.defaultTestLoader.loadTestsFromModule(module)
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
    checks.append({'name': 'offline_helper_and_source_tests',
                   'result': 'PASS' if result.wasSuccessful() else 'FAIL',
                   'testsRun': result.testsRun, 'failures': len(result.failures),
                   'errors': len(result.errors), 'log': stream.getvalue()})

    def smoke_check():
        run = subprocess.run([sys.executable, '-B', str(SKILL / 'scripts/inspect_tools.py'),
                              str(HERE / 'fixtures/tools-list.synthetic.json'), '--tool', 'buffer'],
                             capture_output=True, text=True, encoding='utf-8', env=env, check=False)
        assert run.returncode == 0, run.stderr
        report = json.loads(run.stdout)
        assert report['runtimeExecuted'] is False
        assert report['observedToolCount'] == 2
        assert report['coverage'] == 'PARTIAL_OR_UNATTESTED_EXPORT'
        assert report['missingVsBaseline'] == []
        return {'fixtureStatus': 'SYNTHETIC_NOT_LIVE', 'observedTools': 2,
                'absentBaselineToolsRemainUnknown': True, 'runtimeExecuted': False}
    check('actual_cli_on_synthetic_capture', smoke_check)

    def board_smoke_check():
        run = subprocess.run([sys.executable, '-B', str(SKILL / 'scripts/render_task_board.py'),
                              str(HERE / 'fixtures/task-board.synthetic.json')],
                             capture_output=True, text=True, encoding='utf-8', env=env, check=False)
        assert run.returncode == 0, run.stderr
        assert 'SYNTHETIC_NOT_LIVE' in run.stdout.replace('\\_', '_'), 'Synthetic disclosure is missing.'
        assert '1/8' in run.stdout, 'Verification counter does not match the fixture.'
        assert '100%' not in run.stdout, 'False completion percentage.'
        assert '](<D:' not in run.stdout, 'Planned artifacts became download links.'
        saved = HERE / 'fixtures/task-board.synthetic.md'
        assert saved.read_text(encoding='utf-8') == run.stdout, 'Saved display example is stale.'
        return {'fixtureStatus': 'SYNTHETIC_NOT_LIVE', 'steps': 8, 'verifiedStepsInSyntheticRecord': 1,
                'realGisExecutions': 0, 'savedMarkdownMatchesCli': True}
    check('actual_task_board_cli_and_saved_example', board_smoke_check)

    manifest = []
    for file in sorted(PACKAGE.rglob('*')):
        if file.is_file() and file.resolve() != output:
            manifest.append({'path': file.relative_to(PACKAGE).as_posix(), 'bytes': file.stat().st_size,
                             'sha256': hashlib.sha256(file.read_bytes()).hexdigest().upper()})
    receipt = {
        'kind': 'OFFLINE_SKILL_VALIDATION_CANDIDATE', 'date': __import__('datetime').date.today().isoformat(), 'skillVersion': skill_meta['metadata']['version'],
        'candidateDisposition': 'READY_FOR_INDEPENDENT_REVIEW' if all(c['result'] == 'PASS' for c in checks) else 'CHANGES_NEEDED',
        'acceptance': 'NOT_GRANTED_EXECUTOR_PASS_CANDIDATE_ONLY',
        'checks': checks,
        'behaviorEvaluation': {'definedCases': case_count, 'executedInRealClient': 0,
                               'independentForwardTest': 'NOT RUN', 'runtimePassRate': None},
        'authorDeskReview': {
            'kind': 'SELF_REVIEW_NOT_MODEL_EVAL_OR_INDEPENDENT_ACCEPTANCE',
            'caseIds': ['B06', 'B14', 'B20', 'B24', 'B27', 'B29', 'B33', 'B36', 'B37', 'B38', 'B45', 'B48', 'B49', 'B51', 'B53', 'B56', 'B60', 'B64'],
            'observations': [
                'B06: vector reference resolves CRS/method, preserves the real buffer schema without invented method.',
                'B14: main instructions preserve read-only control; Read classification does not authorize toggling.',
                'B20: allowlist gap remains a support gap, not permission for Python/6511 bypass.',
                'B24: recovery requires job/audit/effect reconciliation before possible write retry.',
                'B27: cartography reference separates layout size from export resolution and requires visual verification.',
                'B29: Photoshop reference blocks false PS/PSD success and does not transfer design labor to the user.',
                'B33/B36: adaptive reference checks现场 conditions, method and per-target binding instead of executing keyword recipes blindly.',
                'B37/B38: live display distinguishes unknown internal progress, backend counters and artifact verification.',
                'B45/B48: plan changes invalidate affected results; synthetic snapshots remain demonstrations, not live evidence.',
                'B49/B56: actual standard clauses and product tolerances are not inferred from catalogs or structural validators.',
                'B51/B53/B64: recheck requires new evidence; sampling and stale reports cannot fulfill full current checks.',
                'B60: all technical checks form a candidate, not executor-granted final acceptance.'
            ]},
        'notRunByThisOfflineValidator': ['Skill installation', 'target client discovery/trigger tests', 'live MCP calls',
                   'GIS dataset writes', 'real map rendering and visual inspection', 'Photoshop/UXP execution',
                   'cross-client interoperability', 'real-client incremental message and image display',
                   'server progress push', 'end-to-end product acceptance'],
        'runtimeEvidenceScope': 'This offline receipt does not summarize separate authorized live self-tests. Refer to the actual live-test report for those results.',
        'changesScope': 'Skill candidate, maintenance and validation only; receipt under explicit D-drive root; no product/backend/release or 3.0 plan edits.',
        'manifestExcludes': 'This receipt itself; hashes detect later changes, not publisher identity or a digital signature.',
        'manifest': manifest}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'candidateDisposition': receipt['candidateDisposition'],
                      'checks': len(checks), 'helperTests': result.testsRun,
                      'behaviorCasesExecutedInRealClient': 0, 'report': str(output)}, ensure_ascii=False))
    return 0 if all(c['result'] == 'PASS' for c in checks) else 1


if __name__ == '__main__':
    raise SystemExit(main())
