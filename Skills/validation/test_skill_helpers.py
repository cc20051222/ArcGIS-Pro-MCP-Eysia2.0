"""Offline checks: no server, GIS, client config, or dependency installation."""
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
import os
import subprocess
import sys
import unittest
from pathlib import Path
from unittest import mock

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
SKILL = HERE.parent / 'arcgis-pro-2-workflows'
REPO = HERE.parents[1]


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


inspector = load_module('skill_inspector', SKILL / 'scripts/inspect_tools.py')
builder = load_module('skill_catalog_builder', HERE.parent / 'maintenance/build_catalog.py')
board = load_module('skill_task_board', SKILL / 'scripts/render_task_board.py')
package_validator = load_module('skill_package_validator', HERE / 'validate_package.py')


def tool(name, schema=None):
    return {'name': name, 'inputSchema': schema or {'type': 'object', 'properties': {}}}


BASE = {'tools': [
    {'name': 'buffer', 'sourceSupportObservation': 'REGISTERED_SOURCE_ONLY_RUNTIME_NOT_TESTED'},
    {'name': 'ps_export_document', 'sourceSupportObservation': 'SOURCE_FAIL_CLOSED_PS_UXP_NOT_IMPLEMENTED'},
]}


def export(*pages):
    return {'complete': True, 'pages': list(pages)}


class ReceiptRootTests(unittest.TestCase):
    def test_explicit_owned_root_accepts_nested_receipt(self):
        root = Path('D:/shiyanwenjaijia')
        self.assertEqual(package_validator.validate_receipt_path(root/'自测记录/result.json', root),
                         (root/'自测记录/result.json').resolve())

    def test_sibling_prefix_is_not_containment(self):
        with self.assertRaises(ValueError):
            package_validator.validate_receipt_path(Path('D:/shiyanwenjaijia-other/result.json'), Path('D:/shiyanwenjaijia'))

    def test_parent_escape_is_not_containment(self):
        with self.assertRaises(ValueError):
            package_validator.validate_receipt_path(Path('D:/shiyanwenjaijia/../outside.json'), Path('D:/shiyanwenjaijia'))

    def test_c_drive_is_rejected_even_when_explicit(self):
        with self.assertRaises(ValueError):
            package_validator.validate_receipt_path(Path('C:/not-written/result.json'), Path('C:/not-written'))

    def test_output_cannot_be_root_directory(self):
        with self.assertRaises(ValueError):
            package_validator.validate_receipt_path(Path('D:/shiyanwenjaijia'), Path('D:/shiyanwenjaijia'))


class CaptureTests(unittest.TestCase):
    def test_single_response_does_not_prove_missing(self):
        report, _ = inspector.inspect({'tools': [tool('buffer')]}, BASE)
        self.assertEqual(report['missingVsBaseline'], [])
        self.assertEqual(report['unobservedStatus'], 'UNKNOWN_NOT_PROVEN_ABSENT')

    def test_complete_one_page_reports_missing(self):
        report, _ = inspector.inspect(export({'cursor': None, 'result': {'tools': [tool('buffer')]}}), BASE)
        self.assertEqual(report['missingVsBaseline'], ['ps_export_document'])

    def test_valid_cursor_chain_combines_pages(self):
        document = export(
            {'cursor': None, 'result': {'tools': [tool('buffer')], 'nextCursor': 'second'}},
            {'cursor': 'second', 'result': {'tools': [tool('ps_export_document')]}})
        report, _ = inspector.inspect(document, BASE)
        self.assertEqual(report['observedToolCount'], 2)
        self.assertFalse(report['runtimeExecuted'])
        self.assertFalse(report['authorizationOrBehaviorProven'])
        self.assertEqual(report['missingVsBaseline'], [])

    def test_complete_unresolved_cursor_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect(export({'cursor': None, 'result': {'tools': [], 'nextCursor': 'missing'}}), BASE)

    def test_complete_wrong_cursor_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect(export(
                {'cursor': None, 'result': {'tools': [], 'nextCursor': 'correct'}},
                {'cursor': 'wrong', 'result': {'tools': []}}), BASE)

    def test_complete_missing_request_cursor_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect(export({'result': {'tools': []}}), BASE)

    def test_extra_page_after_terminal_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect(export(
                {'cursor': None, 'result': {'tools': []}},
                {'cursor': None, 'result': {'tools': []}}), BASE)

    def test_repeated_cursor_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect(export(
                {'cursor': None, 'result': {'tools': [], 'nextCursor': 'again'}},
                {'cursor': 'again', 'result': {'tools': [], 'nextCursor': 'again'}},
                {'cursor': 'again', 'result': {'tools': []}}), BASE)

    def test_empty_cursor_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect({'tools': [], 'nextCursor': ''}, BASE)

    def test_non_string_cursor_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect({'tools': [], 'nextCursor': 7}, BASE)

    def test_duplicate_names_across_pages_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect(export(
                {'cursor': None, 'result': {'tools': [tool('buffer')], 'nextCursor': 'p2'}},
                {'cursor': 'p2', 'result': {'tools': [tool('buffer')]}}), BASE)

    def test_non_object_schema_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect({'tools': [tool('bad', {'type': 'array'})]}, BASE)

    def test_missing_schema_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect({'tools': [{'name': 'bad'}]}, BASE)

    def test_jsonrpc_error_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect({'jsonrpc': '2.0', 'error': {'code': -32000}}, BASE)

    def test_empty_pages_rejected(self):
        with self.assertRaises(ValueError):
            inspector.inspect({'complete': True, 'pages': []}, BASE)

    def test_invalid_top_level_rejected(self):
        for value in (None, [], 2, 'text'):
            with self.subTest(value=value), self.assertRaises(ValueError):
                inspector.inspect(value, BASE)

    def test_invalid_baseline_rejected(self):
        for value in (None, [], {'tools': []}, {'tools': [{'name': 'buffer'}]}):
            with self.subTest(value=value), self.assertRaises(ValueError):
                inspector.inspect({'tools': [tool('buffer')]}, value)

    def test_duplicate_baseline_names_rejected(self):
        duplicate = {'tools': [BASE['tools'][0], BASE['tools'][0]]}
        with self.assertRaises(ValueError):
            inspector.inspect({'tools': []}, duplicate)

    def test_extra_tool_reported_without_execution(self):
        report, _ = inspector.inspect({'tools': [tool('new_tool')]}, BASE)
        self.assertEqual(report['extraVsBaseline'], ['new_tool'])
        self.assertFalse(report['runtimeExecuted'])

    def test_source_limitations_preserved(self):
        report, _ = inspector.inspect({'tools': [tool('ps_export_document')]}, BASE)
        self.assertEqual(report['observedSourceCaveats'][0]['sourceSupportObservation'],
                         'SOURCE_FAIL_CLOSED_PS_UXP_NOT_IMPLEMENTED')

    def test_schema_hash_ignores_object_key_order(self):
        first = {'type': 'object', 'properties': {'a': {'type': 'string'}}, 'required': ['a']}
        second = {'required': ['a'], 'properties': {'a': {'type': 'string'}}, 'type': 'object'}
        a, _ = inspector.inspect({'tools': [tool('buffer', first)]}, BASE)
        b, _ = inspector.inspect({'tools': [tool('buffer', second)]}, BASE)
        self.assertEqual(a['schemas'], b['schemas'])

    def test_inspection_does_not_mutate_capture(self):
        document = {'tools': [tool('buffer')]}
        original = copy.deepcopy(document)
        inspector.inspect(document, BASE)
        self.assertEqual(document, original)

    def test_cli_missing_input_returns_structured_error(self):
        args = ['inspect_tools.py', str(HERE / 'fixtures/intentionally-absent.capture.json')]
        with mock.patch.object(sys, 'argv', args), contextlib.redirect_stderr(io.StringIO()) as errors:
            result = inspector.main()
        self.assertEqual(result, 2)
        self.assertFalse(json.loads(errors.getvalue())['runtimeExecuted'])

    def test_cli_unknown_selected_tool_returns_error(self):
        args = ['inspect_tools.py', str(HERE / 'fixtures/tools-list.synthetic.json'), '--tool', 'absent']
        with mock.patch.object(sys, 'argv', args), contextlib.redirect_stderr(io.StringIO()) as errors:
            result = inspector.main()
        self.assertEqual(result, 2)
        self.assertIn('not present', json.loads(errors.getvalue())['error'])


class TaskBoardTests(unittest.TestCase):
    def record(self):
        return json.loads((HERE / 'fixtures/task-board.synthetic.json').read_text(encoding='utf-8'))

    def test_empty_template_is_not_a_completed_task(self):
        record = json.loads((SKILL / 'assets/task-record.json').read_text(encoding='utf-8'))
        plan, steps, order, evidence, presentation = board.validate(record)
        self.assertEqual(len(order), 0)
        self.assertEqual(evidence, {})
        self.assertNotIn('%', board.render(record))

    def test_dependency_sort_is_independent_of_input_order(self):
        record = self.record()
        record['plan']['steps'].reverse()
        _, _, order, _, _ = board.validate(record)
        self.assertEqual(order, ['S1', 'S2', 'S3', 'S4', 'S5', 'S6', 'S7', 'S8'])

    def test_dependency_cycle_rejected(self):
        record = self.record()
        record['plan']['steps'][0]['dependsOn'] = ['S5']
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_unknown_dependency_rejected(self):
        record = self.record()
        record['plan']['steps'][0]['dependsOn'] = ['unknown']
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_duplicate_step_id_rejected(self):
        record = self.record()
        record['plan']['steps'][1]['id'] = 'S1'
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_unobserved_success_rejected(self):
        record = self.record()
        record['plan']['steps'][1]['evidenceRefs'] = []
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_unknown_observation_reference_rejected(self):
        record = self.record()
        record['plan']['steps'][1]['evidenceRefs'] = ['invented']
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_completion_requires_all_required_checks(self):
        record = self.record()
        record['execution']['status'] = 'COMPLETED'
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_only_verified_required_steps_allow_recorded_completion(self):
        record = self.record()
        record['mode'] = 'inspect'  # Production additionally requires the four quality phases.
        for step in record['plan']['steps']:
            step['state'] = 'VERIFIED'
            step['evidenceRefs'] = ['DEMO-E1']
        record['execution']['status'] = 'COMPLETED'
        board.validate(record)  # Evidence presence is checked, not underlying GIS truth.

    def test_refresh_time_cannot_replace_last_observation(self):
        record = self.record()
        record['presentation']['lastObservedAt'] = '2026-10-18T11:00:00+08:00'
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_observation_without_timezone_rejected(self):
        record = self.record()
        record['observations'][0]['observedAt'] = '2026-10-18T10:00:00'
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_counts_keep_runtime_and_verification_separate(self):
        record = self.record()
        record['presentation']['runtimeCounts'] = {'basis': 'job sample, not full workflow',
            'done': 9, 'failed': 1, 'skipped': 0, 'pending': 0, 'total': 10, 'evidenceRef': 'DEMO-E2'}
        board.validate(record)
        rendered = board.render(record)
        self.assertIn('执行成功 9', rendered)
        self.assertIn('1/8', rendered)
        self.assertNotIn('100%', rendered)

    def test_inconsistent_negative_or_boolean_counts_rejected(self):
        for done in (-1, True, 11):
            record = self.record()
            record['presentation']['runtimeCounts'] = {'basis': 'sample job', 'done': done,
                'failed': 0, 'skipped': 0, 'pending': 0, 'total': 10, 'evidenceRef': 'DEMO-E2'}
            with self.subTest(done=done), self.assertRaises(ValueError):
                board.validate(record)

    def test_unknown_total_is_allowed_without_inventing_percentage(self):
        record = self.record()
        record['presentation']['runtimeCounts'] = {'basis': 'partial sample', 'done': 4,
            'total': None, 'evidenceRef': 'DEMO-E1'}
        self.assertNotIn('%', board.render(record))

    def test_non_d_traversal_and_ads_paths_rejected(self):
        for path in ('C:/out/a.png', '//server/share/a.png', 'D:/out/../secret.png',
                     'D:relative.png', 'D:/out/a.png:stream', 'https://host/a.png'):
            record = self.record()
            record['presentation']['artifacts'][0]['path'] = path
            with self.subTest(path=path), self.assertRaises(ValueError):
                board.validate(record)

    def test_planned_artifact_is_not_a_download_even_if_path_exists(self):
        record = self.record()
        with mock.patch.object(Path, 'exists', return_value=True):
            rendered = board.render(record, include_images=True)
        self.assertNotIn('](<D:', rendered)

    def test_stale_artifact_is_not_a_download(self):
        record = self.record()
        record['presentation']['artifacts'][0]['state'] = 'STALE'
        with mock.patch.object(Path, 'exists', return_value=True):
            self.assertNotIn('](<D:', board.render(record))

    def test_artifact_verified_state_requires_verified_step_and_observation(self):
        record = self.record()
        artifact = record['presentation']['artifacts'][0]
        artifact.update(state='VERIFIED', observationRef='DEMO-E1')
        with self.assertRaises(ValueError):
            board.validate(record)

    def test_untrusted_markdown_cannot_inject_an_image(self):
        record = self.record()
        record['goal'] = '![fake](https://external.test/a.png)\n<script>alert(1)</script>'
        rendered = board.render(record)
        self.assertNotIn('![fake](', rendered)
        self.assertNotIn('<script>', rendered)

    def test_rendering_does_not_mutate_record(self):
        record = self.record()
        original = copy.deepcopy(record)
        board.render(record)
        self.assertEqual(record, original)

    def test_cli_bad_record_returns_error_without_runtime_execution(self):
        with mock.patch.object(sys, 'argv', ['render_task_board.py', str(HERE / 'fixtures/absent.task.json')]), \
                contextlib.redirect_stderr(io.StringIO()) as errors:
            code = board.main()
        self.assertEqual(code, 2)
        self.assertFalse(json.loads(errors.getvalue())['runtimeExecuted'])

    def test_cli_utf8_output_when_windows_utf8_mode_is_disabled(self):
        env = dict(os.environ, PYTHONUTF8='0', PYTHONDONTWRITEBYTECODE='1')
        result = subprocess.run([sys.executable, '-B', str(SKILL / 'scripts/render_task_board.py'),
                                 str(HERE / 'fixtures/task-board.synthetic.json')],
                                capture_output=True, encoding='utf-8', env=env, check=False)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('流程与成果状态板', result.stdout)


class QualityRecordTests(unittest.TestCase):
    def record(self, complete=False):
        record = json.loads((HERE / 'fixtures/task-board.synthetic.json').read_text(encoding='utf-8'))
        if complete:
            record['observations'].extend([
                {'id':'DEMO-E3','source':'SYNTHETIC_RECHECK','observedAt':'2026-10-18T10:00:10+08:00','summary':'new simulated recheck'},
                {'id':'DEMO-E4','source':'SYNTHETIC_FINAL','observedAt':'2026-10-18T10:00:15+08:00','summary':'new simulated final check'}])
            record['presentation']['lastObservedAt'] = '2026-10-18T10:00:15+08:00'
            record['quality']['checks'].extend([
                {'id':'DEMO-Q3','requirementId':'R2','phase':'RECHECK','state':'PASS','method':'new simulated reading',
                 'profileRevision':1,'bindings':{'analysis':'demo-analysis-v1'},
                 'scope':{'mode':'FULL','population':1,'checked':1},'recheckOf':'DEMO-Q2','evidenceRefs':['DEMO-E3']},
                {'id':'DEMO-Q4','requirementId':'R3','phase':'FINAL','state':'PASS','method':'simulated deliverable reconciliation',
                 'profileRevision':1,'bindings':{'delivery':'demo-delivery-v1'},
                 'scope':{'mode':'FULL','population':1,'checked':1},'evidenceRefs':['DEMO-E4']}])
            for step in record['plan']['steps']:
                if step.get('required', True):
                    step.update(state='VERIFIED', evidenceRefs=['DEMO-E4'])
        return record

    def summary(self, record):
        _, _, _, evidence, _ = board.validate(record)
        return board.quality_summary(record, evidence)

    def acceptance(self, record):
        record['observations'].append({'id':'DEMO-A1','source':'SYNTHETIC_EXTERNAL_RECEIPT',
            'observedAt':'2026-10-18T10:00:20+08:00','summary':'simulated external acceptance; not a real authorization'})
        record['presentation']['lastObservedAt'] = '2026-10-18T10:00:20+08:00'
        record['quality']['acceptance'] = {'status':'ACCEPTED','reviewerRole':'USER',
            'reviewerId':'demo-user','evidenceRefs':['DEMO-A1']}

    def test_pending_recheck_and_final_are_not_ready(self):
        result = self.summary(self.record())
        self.assertEqual(result['gates']['PROCESS']['state'], 'PASS')
        self.assertEqual(result['gates']['RECHECK']['state'], 'NOT_RUN')
        self.assertEqual(result['candidate'], 'NOT_READY')

    def test_empty_scope_explanation_is_visible_in_chat_and_projection(self):
        WorkflowPublisherTests.setUpClass()
        record = self.record(complete=True)
        record['quality']['requirements'][0]['allowEmpty'] = True
        record['quality']['checks'][0]['scope'] = {
            'mode':'FULL','population':0,'checked':0,'emptyRationale':'Explicit empty output condition'}
        rendered = board.render(record)
        self.assertIn('允许空范围（须有适用说明）', rendered)
        self.assertIn('空范围说明：Explicit empty output condition', rendered)
        view = WorkflowPublisherTests.publisher.project(record)
        self.assertEqual(view['checks'][0]['scope']['emptyRationale'], 'Explicit empty output condition')

    def test_complete_checks_make_candidate_not_external_acceptance(self):
        result = self.summary(self.record(complete=True))
        self.assertEqual(result['candidate'], 'PASS_CANDIDATE')
        self.assertEqual(result['acceptance'], 'NOT_REQUESTED')

    def test_empty_requirements_cannot_pass(self):
        record = self.record()
        record['quality']['requirements'] = []
        record['quality']['checks'] = []
        self.assertEqual(self.summary(record)['gates']['FINAL']['state'], 'NOT_CONFIGURED')

    def test_incoming_pass_flags_do_not_override_missing_checks(self):
        record = self.record()
        record['quality']['gates'] = {p: {'state':'PASS'} for p in board.PHASES}
        self.assertEqual(self.summary(record)['candidate'], 'NOT_READY')

    def test_reusing_initial_evidence_is_not_a_recheck(self):
        record = self.record(complete=True)
        record['quality']['checks'][2]['evidenceRefs'] = ['DEMO-E2']
        self.assertEqual(self.summary(record)['checkStates']['DEMO-Q3'], 'RECHECK_EVIDENCE_INSUFFICIENT')

    def test_missing_initial_link_blocks_recheck(self):
        record = self.record(complete=True)
        record['quality']['checks'][2].pop('recheckOf')
        self.assertEqual(self.summary(record)['candidate'], 'NOT_READY')

    def test_final_check_cannot_predate_recheck(self):
        record = self.record(complete=True)
        record['quality']['checks'][3]['evidenceRefs'] = ['DEMO-E1']
        self.assertEqual(self.summary(record)['checkStates']['DEMO-Q4'], 'FINAL_EVIDENCE_PREDATES_RECHECK')

    def test_sample_cannot_satisfy_full_coverage(self):
        record = self.record(complete=True)
        record['quality']['checks'][2]['scope'] = {'mode':'SAMPLE','population':100,'checked':5,'sampleDesign':'simulated sample'}
        self.assertEqual(self.summary(record)['checkStates']['DEMO-Q3'], 'INSUFFICIENT_COVERAGE')

    def test_allowed_sampling_still_needs_declared_design(self):
        record = self.record(complete=True)
        record['quality']['requirements'][1]['coverageRequired'] = 'SAMPLE_ALLOWED'
        record['quality']['checks'][2]['scope'] = {'mode':'SAMPLE','population':100,'checked':5}
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_full_check_with_unknown_population_is_insufficient(self):
        record = self.record(complete=True)
        record['quality']['checks'][2]['scope']['population'] = None
        self.assertEqual(self.summary(record)['candidate'], 'NOT_READY')

    def test_empty_full_scope_cannot_implicitly_pass(self):
        record = self.record(complete=True)
        record['quality']['checks'][0]['scope'] = {'mode':'FULL','population':0,'checked':0}
        result = self.summary(record)
        self.assertEqual(result['checkStates']['DEMO-Q1'], 'INSUFFICIENT_COVERAGE')
        self.assertEqual(result['gates']['PREFLIGHT']['state'], 'BLOCKED')
        self.assertEqual(result['candidate'], 'NOT_READY')

    def test_empty_scope_needs_requirement_policy_and_observed_rationale(self):
        for allow_empty, rationale in ((False, 'No matches were expected'), (True, None),
                                       (True, ''), (True, '   ')):
            with self.subTest(allow_empty=allow_empty, rationale=rationale):
                record = self.record(complete=True)
                record['quality']['requirements'][0]['allowEmpty'] = allow_empty
                record['quality']['checks'][0]['scope'] = {
                    'mode':'FULL','population':0,'checked':0,'emptyRationale':rationale}
                self.assertEqual(self.summary(record)['checkStates']['DEMO-Q1'], 'INSUFFICIENT_COVERAGE')

    def test_explicitly_allowed_empty_scope_preserves_other_quality_gates(self):
        record = self.record(complete=True)
        record['quality']['requirements'][0].update(
            allowEmpty=True, criterion='A full source scan may return no matches for this criterion',
            source='SYNTHETIC_GOAL_NOT_AUTHORIZATION')
        record['quality']['checks'][0]['scope'] = {
            'mode':'FULL','population':0,'checked':0,
            'emptyRationale':'Synthetic observation confirms no matches, allowed by this criterion.'}
        result = self.summary(record)
        self.assertEqual(result['checkStates']['DEMO-Q1'], 'PASS')
        self.assertEqual(result['candidate'], 'PASS_CANDIDATE')
        self.assertEqual(result['acceptance'], 'NOT_REQUESTED')
        record['quality']['checks'][0]['evidenceRefs'] = []
        with self.assertRaisesRegex(ValueError, 'recorded evidence'):
            self.summary(record)

    def test_empty_policy_requires_a_real_boolean(self):
        for value in ('true', 1, None):
            with self.subTest(value=value):
                record = self.record(complete=True)
                record['quality']['requirements'][0]['allowEmpty'] = value
                with self.assertRaisesRegex(ValueError, 'allowEmpty must be boolean'):
                    self.summary(record)

    def test_empty_scope_never_bypasses_stale_bindings(self):
        record = self.record(complete=True)
        record['quality']['requirements'][0]['allowEmpty'] = True
        record['quality']['checks'][0]['scope'] = {
            'mode':'FULL','population':0,'checked':0,'emptyRationale':'Allowed empty result'}
        record['quality']['subjectVersions']['source'] = 'updated-source-v2'
        self.assertEqual(self.summary(record)['checkStates']['DEMO-Q1'], 'STALE')

    def test_new_artifact_version_invalidates_relevant_checks(self):
        record = self.record(complete=True)
        record['quality']['subjectVersions']['analysis'] = 'new-user-edited-version'
        result = self.summary(record)
        self.assertEqual(result['gates']['RECHECK']['state'], 'STALE')
        self.assertEqual(result['gates']['PREFLIGHT']['state'], 'PASS')

    def test_unreviewed_new_plan_invalidates_quality(self):
        record = self.record(complete=True)
        record['plan']['revision'] = 2
        self.assertEqual(self.summary(record)['gates']['FINAL']['state'], 'STALE')

    def test_open_severe_issue_blocks_candidate(self):
        record = self.record(complete=True)
        record['quality']['issues'] = [{'id':'I1','severity':'MAJOR','status':'OPEN','description':'required member mismatch'}]
        self.assertEqual(self.summary(record)['candidate'], 'NOT_READY')

    def test_issue_cannot_close_using_only_initial_evidence(self):
        record = self.record(complete=True)
        record['quality']['issues'] = [{'id':'I1','severity':'MAJOR','status':'RESOLVED',
            'description':'mismatch','resolutionEvidenceRefs':['DEMO-E2']}]
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_severe_issue_cannot_be_accepted_as_a_minor_residual(self):
        record = self.record(complete=True)
        record['quality']['issues'] = [{'id':'I1','severity':'CRITICAL','status':'ACCEPTED_RESIDUAL',
            'description':'wrong CRS','acceptedByRole':'USER','acceptanceEvidenceRefs':['DEMO-E4']}]
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_executor_cannot_self_accept(self):
        record = self.record(complete=True)
        self.acceptance(record)
        record['quality']['acceptance']['reviewerRole'] = 'EXECUTOR'
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_same_actor_cannot_claim_independent_acceptance(self):
        record = self.record(complete=True)
        self.acceptance(record)
        record['quality']['acceptance']['reviewerId'] = 'demo-producer'
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_external_receipt_cannot_be_replaced_by_a_technical_check(self):
        record = self.record(complete=True)
        self.acceptance(record)
        record['quality']['acceptance']['evidenceRefs'] = ['DEMO-E4']
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_external_receipt_can_be_displayed_without_granting_real_acceptance(self):
        record = self.record(complete=True)
        self.acceptance(record)
        self.assertEqual(self.summary(record)['acceptance'], 'ACCEPTED')
        self.assertIn('离线格式检查不授予最终 PASS', board.render(record))

    def test_duplicate_active_attempts_cannot_hide_failures(self):
        record = self.record(complete=True)
        extra = copy.deepcopy(record['quality']['checks'][0])
        extra['id'] = 'alternate-passing-check'
        record['quality']['checks'].append(extra)
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_production_completion_requires_quality_as_well_as_step_states(self):
        record = self.record()
        for step in record['plan']['steps']:
            step.update(state='VERIFIED', evidenceRefs=['DEMO-E2'])
        record['execution']['status'] = 'COMPLETED'
        with self.assertRaises(ValueError):
            self.summary(record)

    def test_unfinished_required_step_blocks_technical_candidate(self):
        record = self.record(complete=True)
        record['plan']['steps'][-2].update(state='PLANNED', evidenceRefs=[])
        self.assertEqual(self.summary(record)['candidate'], 'NOT_READY')


class SourceReferenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = json.loads((SKILL / 'references/tool-catalog.snapshot.json').read_text(encoding='utf-8'))

    def test_catalog_rebuild_matches_current_source(self):
        fresh = builder.build(REPO, self.catalog['generatedOn'])
        self.assertEqual(fresh, self.catalog)

    def test_source_evidence_files_still_match(self):
        for evidence in self.catalog['sources']:
            with self.subTest(path=evidence['path']):
                actual = builder.sha(REPO / evidence['path'])
                self.assertEqual(actual, evidence['sha256'])

    def test_workflow_interface_observations_still_match_source(self):
        research = json.loads((HERE / 'research-sources.json').read_text(encoding='utf-8'))
        for evidence in research['localInterfaceObservations']:
            with self.subTest(path=evidence['path']):
                actual = builder.sha(REPO / evidence['path'])
                self.assertEqual(actual, evidence['sha256'])

    def test_registration_and_channel_invariants(self):
        self.assertEqual(self.catalog['registeredToolCount'], 239)
        self.assertEqual(len({r['name'] for r in self.catalog['tools']}), 239)
        self.assertEqual(self.catalog['sourceContractNameDifference'], [])
        self.assertEqual(self.catalog['executionTypeCounts'],
                         {'Native': 156, 'Geoprocessing': 56, 'Python': 7, 'Bridge': 20})
        self.assertEqual(len(set(self.catalog['errorCodes'])), 33)
        self.assertEqual(self.catalog['gpWhitelist']['count'], 53)

    def test_support_observations_are_version_bound(self):
        by_name = {t['name']: t for t in self.catalog['tools']}
        self.assertEqual(sum(t['sourceSupportObservation'] == 'SOURCE_FAIL_CLOSED_PS_UXP_NOT_IMPLEMENTED'
                             for t in by_name.values()), 19)
        real_sha = builder.sha
        def changed_ps_source(path):
            return 'CHANGED_FOR_OFFLINE_TEST' if path.name in builder.REVIEWED_SUPPORT_SOURCES else real_sha(path)
        with mock.patch.object(builder, 'sha', side_effect=changed_ps_source):
            changed = builder.build(REPO, self.catalog['generatedOn'])
        changed_by_name = {t['name']: t for t in changed['tools']}
        self.assertEqual(changed_by_name['ps_get_capabilities']['sourceSupportObservation'],
                         'SOURCE_CHANGED_SUPPORT_REVIEW_REQUIRED')
        self.assertEqual(changed_by_name['export_time_animation']['sourceSupportObservation'],
                         'SOURCE_CHANGED_SUPPORT_REVIEW_REQUIRED')


class WorkflowPublisherTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.publisher = load_module('workflow_publisher', SKILL / 'scripts/publish_workflow.py')

    def record(self):
        r=json.loads((SKILL/'assets/task-record.json').read_text(encoding='utf-8'))
        r.update(taskId='task-one',goal='Actual user workflow')
        r['plan']['steps']=[{'id':'A','title':'Inspect','state':'PLANNED','dependsOn':[],'evidenceRefs':[],
                             'kind':'PREFLIGHT','requestIds':['task-one-A']}]
        return r

    def test_full_plan_not_candidate_or_execution(self):
        v=self.publisher.project(self.record())
        self.assertEqual(v['steps'][0]['state'],'PLANNED')
        self.assertEqual(v['candidate'],'NOT_READY')
        self.assertIsNone(v['observedAt'])

    def test_running_requires_dispatch_evidence(self):
        r=self.record();r['plan']['steps'][0]['state']='RUNNING'
        for surface in (board.render, self.publisher.project):
            with self.subTest(surface=surface.__name__), self.assertRaisesRegex(ValueError, 'RUNNING requires'):
                surface(r)

    def test_duplicate_request_binding_rejected(self):
        r=self.record();r['plan']['steps'].append({**r['plan']['steps'][0],'id':'B'})
        for surface in (board.render, self.publisher.project):
            with self.subTest(surface=surface.__name__), self.assertRaisesRegex(ValueError, 'request ID'):
                surface(r)

    def test_started_step_with_unverified_dependency_rejected(self):
        r=self.record();r['observations']=[{'id':'dispatch','observedAt':'2026-10-18T10:00:00+08:00','source':'actual_dispatch','summary':'request sent'}]
        # PREFLIGHT isolates dependencies from the separate missing-intake guard.
        r['plan']['steps'].append({'id':'B','kind':'PREFLIGHT','title':'Run','state':'RUNNING','dependsOn':['A'],'evidenceRefs':['dispatch']})
        for surface in (board.render, self.publisher.project):
            with self.subTest(surface=surface.__name__), self.assertRaisesRegex(ValueError, 'verified dependencies'):
                surface(r)

    def test_unknown_requirement_rejected(self):
        r=self.record();r['plan']['steps'][0]['requirementIds']=['unknown']
        for surface in (board.render, self.publisher.project):
            with self.subTest(surface=surface.__name__), self.assertRaisesRegex(ValueError, 'requirementIds'):
                surface(r)

    def test_c_drive_outputs_rejected(self):
        r=self.record();r['plan']['steps'][0]['outputs']=['C:/forbidden/a.tif']
        # Legacy records also need the output-path guard, despite having no intake root.
        r['schemaVersion']='1.2';r.pop('intake')
        for surface in (board.render, self.publisher.project):
            with self.subTest(surface=surface.__name__), self.assertRaisesRegex(ValueError, 'D-drive'):
                surface(r)

    def test_unknown_step_kind_rejected_on_both_surfaces(self):
        r=self.record();r['plan']['steps'][0]['kind']='INVENTED'
        for surface in (board.render, self.publisher.project):
            with self.subTest(surface=surface.__name__), self.assertRaisesRegex(ValueError, 'step kind'):
                surface(r)

    def test_verified_dependency_allows_observed_preflight_on_both_surfaces(self):
        r=self.record()
        r['observations']=[{'id':'start','observedAt':'2026-10-23T07:00:00+08:00','source':'synthetic_start','summary':'No GIS executed'}]
        r['plan']['steps'][0].update(state='VERIFIED',evidenceRefs=['start'])
        r['plan']['steps'].append({'id':'B','kind':'PREFLIGHT','title':'Inspect next','state':'RUNNING',
                                  'dependsOn':['A'],'evidenceRefs':['start'],'requestIds':['task-one-B']})
        self.assertIn('Inspect next',board.render(r))
        self.assertEqual(self.publisher.project(r)['steps'][1]['state'],'RUNNING')

    def test_repair_problem_dependency_remains_allowed(self):
        for state in ('FAILED','BLOCKED','STALE','VERIFIED'):
            with self.subTest(state=state):
                r=TaskIntakeTests().record()
                r['plan']['steps'][0]['state']=state
                r['plan']['steps'].append({'id':'B','kind':'REPAIR','title':'Repair affected output','state':'RUNNING',
                                          'dependsOn':['A'],'evidenceRefs':['T'],'requestIds':['task-one-B']})
                r['intake']['toolChoices'].append({**r['intake']['toolChoices'][0],'stepId':'B'})
                self.assertIn('Repair affected output',board.render(r))
                self.assertEqual(self.publisher.project(r)['steps'][1]['state'],'RUNNING')

    def test_repair_cannot_overlap_running_dependency(self):
        r=TaskIntakeTests().record()
        r['plan']['steps'].append({'id':'B','kind':'REPAIR','title':'Repair','state':'RUNNING',
                                  'dependsOn':['A'],'evidenceRefs':['T'],'requestIds':['task-one-B']})
        r['intake']['toolChoices'].append({**r['intake']['toolChoices'][0],'stepId':'B'})
        for surface in (board.render, self.publisher.project):
            with self.subTest(surface=surface.__name__), self.assertRaisesRegex(ValueError, 'verified dependencies'):
                surface(r)

    def test_quality_failure_is_not_hidden(self):
        r=QualityRecordTests().record()
        r['plan']['steps'][1]['state']='PLANNED'
        r['quality']['checks'][1]['state']='FAIL'
        v=self.publisher.project(r)
        self.assertEqual(v['candidate'],'NOT_READY')
        self.assertTrue(any(c['effectiveState']=='FAIL' for c in v['checks']))

    def test_atomic_d_publish_history_and_explicit_task_switch(self):
        import tempfile
        owned=REPO/'.runtime/progress-panel/workflow-panel/publisher-tests';owned.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(dir=owned) as d:
            target=Path(d)/'active.json';r=self.record()
            self.publisher.publish(r,target)
            switched=copy.deepcopy(r);switched['taskId']='task-two'
            with self.assertRaises(ValueError):self.publisher.publish(switched,target)
            self.publisher.publish(switched,target,expected_task='task-one',activate=True)
            self.assertEqual(json.loads(target.read_text(encoding='utf-8'))['taskId'],'task-two')
            self.assertEqual(len(list((Path(d)/'active-history').glob('*.json'))),1)
            self.assertFalse(target.with_name('active.json.publish-lock').exists())

    def test_observation_ids_cannot_be_rewritten(self):
        import tempfile
        owned=REPO/'.runtime/progress-panel/workflow-panel/publisher-tests';owned.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(dir=owned) as d:
            original=self.record()
            original['observations']=[{'id':'failure-one','source':'SYNTHETIC_NOT_LIVE',
                'observedAt':'2026-10-22T15:00:00+08:00','summary':'An observed failure'}]
            for field,value in [('summary','Rewritten as success'),('source','rewritten source'),
                                ('observedAt','2026-10-22T15:01:00+08:00')]:
                with self.subTest(field=field):
                    target=Path(d)/(field+'.json');self.publisher.publish(original,target);before=target.read_bytes()
                    changed=copy.deepcopy(original);changed['observations'][0][field]=value
                    with self.assertRaises(ValueError):self.publisher.publish(changed,target)
                    self.assertEqual(target.read_bytes(),before)
                    self.assertFalse(target.with_name(target.name+'.publish-lock').exists())

    def test_correction_appends_new_observation_and_retains_original(self):
        import tempfile
        owned=REPO/'.runtime/progress-panel/workflow-panel/publisher-tests';owned.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(dir=owned) as d:
            target=Path(d)/'active.json';original=self.record()
            original['observations']=[{'id':'failure-one','source':'SYNTHETIC_NOT_LIVE',
                'observedAt':'2026-10-22T15:00:00+08:00','summary':'An observed failure'}]
            self.publisher.publish(original,target);before=target.read_bytes()
            corrected=copy.deepcopy(original);corrected['observations'].append({'id':'correction-two',
                'source':'SYNTHETIC_NOT_LIVE','observedAt':'2026-10-22T15:01:00+08:00',
                'summary':'A later correction; does not erase earlier failure','supersedes':'failure-one'})
            self.publisher.publish(corrected,target);saved=json.loads(target.read_text(encoding='utf-8'))
            self.assertEqual(saved['observations'],corrected['observations'])
            self.assertEqual(saved['workflowView']['steps'][0]['state'],'PLANNED')
            history=list((Path(d)/'active-history').glob('*.json'))
            self.assertEqual(len(history),1);self.assertEqual(history[0].read_bytes(),before)

    def test_removing_observation_leaves_active_record_unchanged(self):
        import tempfile
        owned=REPO/'.runtime/progress-panel/workflow-panel/publisher-tests';owned.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(dir=owned) as d:
            target=Path(d)/'active.json';original=self.record()
            original['observations']=[{'id':'failure-one','source':'SYNTHETIC_NOT_LIVE',
                'observedAt':'2026-10-22T15:00:00+08:00','summary':'An observed failure'}]
            self.publisher.publish(original,target);before=target.read_bytes()
            removed=copy.deepcopy(original);removed['observations']=[]
            with self.assertRaises(ValueError):self.publisher.publish(removed,target)
            self.assertEqual(target.read_bytes(),before)
            self.assertFalse(target.with_name(target.name+'.publish-lock').exists())
            self.assertFalse(list(Path(d).glob('.workflow-*.json')))

    def test_writer_lock_never_removed_by_another_writer(self):
        import tempfile
        owned=REPO/'.runtime/progress-panel/workflow-panel/publisher-tests';owned.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(dir=owned) as d:
            target=Path(d)/'active.json';lock=target.with_name('active.json.publish-lock');lock.write_text('other writer')
            with self.assertRaises(FileExistsError):self.publisher.publish(self.record(),target)
            self.assertEqual(lock.read_text(),'other writer')

    def test_old_plan_cannot_replace_new_plan(self):
        import tempfile
        owned=REPO/'.runtime/progress-panel/workflow-panel/publisher-tests';owned.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(dir=owned) as d:
            target=Path(d)/'active.json';r=self.record();r['plan']['revision']=2;r['quality']['boundPlanRevision']=2
            self.publisher.publish(r,target)
            r['plan']['revision']=1
            with self.assertRaises(ValueError):self.publisher.publish(r,target)


class QualityContractUpdateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        WorkflowPublisherTests.setUpClass()
        cls.publisher = WorkflowPublisherTests.publisher

    @contextlib.contextmanager
    def target(self):
        import tempfile
        owned = (REPO / '.runtime/progress-panel/workflow-panel/quality-contract-tests').resolve()
        owned.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=owned) as directory:
            path = Path(directory).resolve()
            self.assertTrue(path.is_relative_to(owned))
            try:
                yield path / 'active.json'
            finally:
                self.assertTrue(path.resolve().is_relative_to(owned))

    def test_checked_contract_changes_require_new_profile_revision(self):
        mutations = {
            'criterion': lambda q: q['requirements'][0].update(criterion='Changed criterion'),
            'source': lambda q: q['requirements'][0].update(source='Different source'),
            'required': lambda q: q['requirements'][1].update(required=False),
            'coverage': lambda q: q['requirements'][0].update(coverageRequired='SAMPLE_ALLOWED'),
            'allowEmpty': lambda q: q['requirements'][0].update(allowEmpty=True),
            'profile': lambda q: q.update(profile='Different quality profile'),
            'standards': lambda q: q.update(standardMappings=[{'source':'Different standard','scope':'synthetic'}]),
        }
        for name, change in mutations.items():
            with self.subTest(field=name), self.target() as target:
                r = QualityRecordTests().record(complete=True)
                self.publisher.publish(r, target)
                original = target.read_bytes()
                changed = copy.deepcopy(r); change(changed['quality'])
                with self.assertRaisesRegex(ValueError, 'Quality contract changes require a higher profileRevision'):
                    self.publisher.publish(changed, target)
                self.assertEqual(target.read_bytes(), original)
                self.assertFalse(target.with_name(target.name + '.publish-lock').exists())
                self.assertEqual(list(target.parent.glob('.workflow-*')), [])

    def test_profile_revision_cannot_roll_back(self):
        with self.target() as target:
            r = QualityRecordTests().record(complete=True)
            r['quality']['profileRevision'] = 2
            self.publisher.publish(r, target)
            original = target.read_bytes()
            changed = copy.deepcopy(r); changed['quality']['profileRevision'] = 1
            with self.assertRaisesRegex(ValueError, 'Cannot roll back the quality profileRevision'):
                self.publisher.publish(changed, target)
            self.assertEqual(target.read_bytes(), original)

    def test_new_profile_invalidates_old_checks_and_preserves_snapshot(self):
        with self.target() as target:
            r = QualityRecordTests().record(complete=True)
            self.publisher.publish(r, target); original = target.read_bytes()
            changed = copy.deepcopy(r)
            changed['quality']['requirements'][0]['allowEmpty'] = True
            changed['quality']['profileRevision'] += 1
            result = self.publisher.publish(changed, target)
            published = json.loads(target.read_text(encoding='utf-8'))
            self.assertEqual(result['candidate'], 'NOT_READY')
            self.assertTrue(all(c['effectiveState'] == 'STALE' for c in published['workflowView']['checks']))
            self.assertEqual(published['quality']['checks'], r['quality']['checks'])
            archive = target.parent / 'active-history' / (hashlib.sha256(original).hexdigest() + '.json')
            self.assertEqual(archive.read_bytes(), original)

    def test_unchecked_draft_contract_can_be_refined(self):
        with self.target() as target:
            r = WorkflowPublisherTests().record()
            self.publisher.publish(r, target)
            changed = copy.deepcopy(r)
            changed['quality'].update(profile='Refined draft', standardMappings=['Draft standard'])
            result = self.publisher.publish(changed, target)
            self.assertEqual(result['candidate'], 'NOT_READY')
            self.assertEqual(json.loads(target.read_text(encoding='utf-8'))['quality']['profileRevision'], 1)

    def test_data_version_update_does_not_require_new_quality_definition(self):
        with self.target() as target:
            r = QualityRecordTests().record(complete=True)
            self.publisher.publish(r, target)
            changed = copy.deepcopy(r)
            changed['quality']['subjectVersions']['source'] = 'source-data-v2'
            result = self.publisher.publish(changed, target)
            published = json.loads(target.read_text(encoding='utf-8'))
            self.assertEqual(published['quality']['profileRevision'], 1)
            self.assertEqual(published['workflowView']['checks'][0]['effectiveState'], 'STALE')
            self.assertEqual(result['candidate'], 'NOT_READY')

    def test_titles_order_and_explicit_defaults_do_not_invalidate_contract(self):
        with self.target() as target:
            r = QualityRecordTests().record(complete=True)
            self.publisher.publish(r, target)
            changed = copy.deepcopy(r)
            changed['quality']['requirements'].reverse()
            for req in changed['quality']['requirements']:
                req['title'] += ' (display clarification)'
                req['phases'].reverse()
                req['allowEmpty'] = False
            result = self.publisher.publish(changed, target)
            self.assertEqual(result['candidate'], 'PASS_CANDIDATE')

    def test_completed_history_also_protects_the_contract(self):
        with self.target() as target:
            r = QualityRecordTests().record(complete=True)
            r['quality']['history'] = [{'check': copy.deepcopy(c)} for c in r['quality']['checks']]
            for check in r['quality']['checks']:
                check['state'] = 'NOT_RUN'
            self.publisher.publish(r, target); original = target.read_bytes()
            changed = copy.deepcopy(r)
            changed['quality']['requirements'][0]['allowEmpty'] = True
            with self.assertRaisesRegex(ValueError, 'Quality contract changes require a higher profileRevision'):
                self.publisher.publish(changed, target)
            self.assertEqual(target.read_bytes(), original)


class TaskIntakeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        WorkflowPublisherTests.setUpClass()

    def record(self):
        r = WorkflowPublisherTests().record()
        r['observations'] = [{'id':'U','source':'user request','actor':'USER','summary':'Use these inputs and D output', 'observedAt':'2026-10-19T05:00:00+08:00'},
                             {'id':'T','source':'live tools/schema','summary':'tool schema and implementation observed','observedAt':'2026-10-19T05:00:01+08:00'}]
        r['intake'] = {'requirements':{'status':'CONFIRMED','value':'buffer','evidenceRefs':['U']},
                       'inputs':{'status':'CONFIRMED','value':['D:/input/a.shp'],'evidenceRefs':['U']},
                       'outputRoot':{'status':'CONFIRMED','value':'D:/out','evidenceRefs':['U']},
                       'dataNeeds':[], 'toolChoices':[{'stepId':'A','status':'SUPPORTED','tools':['buffer'],'reason':'schema and licence observed','evidenceRefs':['T']}]}
        r['plan']['steps'][0].update(kind='PROCESS',state='RUNNING',evidenceRefs=['T'],outputs=['D:/out/a.gdb/b'])
        return r

    def test_chat_and_panel_share_intake_and_current_step(self):
        r=self.record();v=WorkflowPublisherTests.publisher.project(r);m=board.render(r)
        self.assertEqual(v['intake']['status'],'READY');self.assertIn('D:/out',m);self.assertIn('▶ 当前执行步骤',m)

    def test_missing_intake_blocks_production(self):
        r=self.record();r['intake']['outputRoot']['status']='PROVIDED'
        with self.assertRaises(ValueError):WorkflowPublisherTests.publisher.project(r)
        r=self.record();r['schemaVersion']='1.3';r.pop('intake')
        with self.assertRaises(ValueError):board.validate(r)

    def test_missing_required_data_blocks_dependent_step(self):
        r=self.record();r['intake']['dataNeeds']=[{'id':'DEM','label':'DEM','required':True,'reason':'flow direction','recommendation':'Provide projected DEM with Z units','state':'MISSING','stepIds':['A']}]
        with self.assertRaises(ValueError):board.validate(r)

    def test_independent_readonly_preflight_can_continue(self):
        r=self.record();r['plan']['steps'][0]['kind']='PREFLIGHT';r['intake']['requirements']['status']='MISSING';r['intake']['requirements']['value']=None
        self.assertEqual(WorkflowPublisherTests.publisher.project(r)['intake']['status'],'WAITING_USER')

    def test_substitute_requires_equivalence_and_limits(self):
        r=self.record();r['intake']['toolChoices'][0]['status']='SUBSTITUTE'
        with self.assertRaises(ValueError):board.validate(r)
        r['intake']['toolChoices'][0].update(equivalence='Same planar semantics at metre CRS',limits='Does not support geodesic input')
        board.validate(r)

    def test_development_consent_is_not_executable_capability(self):
        r=self.record();r['intake']['toolChoices'][0].update(status='DEVELOPMENT_PENDING',alternativesConsidered='Existing tools lack D8 semantics',developmentConsentRef='U')
        with self.assertRaises(ValueError):board.validate(r)

    def test_development_consent_cannot_be_tool_observation(self):
        r=self.record();r['plan']['steps'][0]['state']='PLANNED';r['intake']['toolChoices'][0].update(status='DEVELOPMENT_PENDING',alternativesConsidered='none equivalent',developmentConsentRef='T')
        with self.assertRaises(ValueError):board.validate(r)

    def test_planned_and_actual_output_must_stay_under_user_root(self):
        r=self.record();r['plan']['steps'][0]['outputs']=['D:/outside/a.shp']
        with self.assertRaises(ValueError):board.validate(r)

    @contextlib.contextmanager
    def junction_fixture(self):
        """Real Windows aliases, confined to an owned D root; unlink without recursion."""
        import tempfile
        owned = REPO / '.runtime/progress-panel/workflow-panel/junction-tests'
        owned.mkdir(parents=True, exist_ok=True)
        root = Path(tempfile.mkdtemp(dir=owned)).resolve()
        allowed, outside = root / 'allowed', root / 'outside'
        inside = allowed / 'inside'
        for directory in (allowed, outside, inside):
            directory.mkdir()
        links = (allowed / 'escape', allowed / 'internal', root / 'root-alias')
        try:
            command = r"""
$r=$env:SKILL_JUNCTION_TEST_ROOT
New-Item -ItemType Junction -Path (Join-Path $r 'allowed\escape') -Target (Join-Path $r 'outside') -ErrorAction Stop | Out-Null
New-Item -ItemType Junction -Path (Join-Path $r 'allowed\internal') -Target (Join-Path $r 'allowed\inside') -ErrorAction Stop | Out-Null
New-Item -ItemType Junction -Path (Join-Path $r 'root-alias') -Target (Join-Path $r 'allowed') -ErrorAction Stop | Out-Null
"""
            result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command', command],
                                    env={**os.environ, 'SKILL_JUNCTION_TEST_ROOT': str(root)},
                                    capture_output=True, text=True, timeout=30,
                                    creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            self.assertEqual(result.returncode, 0, result.stderr)
            yield allowed, links
        finally:
            for link in links:
                if link.is_junction():
                    self.assertTrue(link.parent == root or link.parent == allowed)
                    self.assertTrue(link.resolve().is_relative_to(root))
                    os.rmdir(link)
            # All remaining directories are known and empty; never recurse over aliases.
            for directory in (inside, outside, allowed, root):
                directory.rmdir()

    @unittest.skipUnless(os.name == 'nt', 'Requires real Windows junctions.')
    def test_same_drive_junction_escape_rejected_in_chat_and_panel(self):
        with self.junction_fixture() as (allowed, links):
            escaped = (links[0] / 'result.gdb/features').as_posix()
            self.assertFalse(Path(escaped).resolve().is_relative_to(allowed.resolve()))
            for output_kind in ('planned', 'artifact'):
                for name, render in (('panel', WorkflowPublisherTests.publisher.project), ('chat', board.render)):
                    with self.subTest(output_kind=output_kind, surface=name):
                        r = self.record()
                        r['intake']['outputRoot']['value'] = allowed.as_posix()
                        r['plan']['steps'][0]['outputs'] = []
                        if output_kind == 'planned':
                            r['plan']['steps'][0]['outputs'] = [escaped]
                        else:
                            r['presentation']['artifacts'] = [
                                {'label': 'planned result', 'stepId': 'A', 'state': 'PLANNED', 'path': escaped}]
                        with self.assertRaisesRegex(ValueError, 'resolves outside the confirmed output root'):
                            render(r)

    @unittest.skipUnless(os.name == 'nt', 'Requires real Windows junctions.')
    def test_internal_junction_preserves_displayed_output_path(self):
        with self.junction_fixture() as (allowed, links):
            value = (links[1] / 'result.gdb/features').as_posix()
            r = self.record()
            r['intake']['outputRoot']['value'] = allowed.as_posix()
            r['plan']['steps'][0]['outputs'] = [value]
            r['presentation']['artifacts'] = [
                {'label': 'planned result', 'stepId': 'A', 'state': 'PLANNED', 'path': value}]
            v = WorkflowPublisherTests.publisher.project(r)
            self.assertEqual(v['steps'][0]['outputs'], [value])
            self.assertIn(board.text(value), board.render(r))

    @unittest.skipUnless(os.name == 'nt', 'Requires real Windows junctions.')
    def test_explicit_root_alias_accepts_its_own_contained_output(self):
        with self.junction_fixture() as (_, links):
            value = (links[2] / 'result.gdb/features').as_posix()
            r = self.record()
            r['intake']['outputRoot']['value'] = links[2].as_posix()
            r['plan']['steps'][0]['outputs'] = [value]
            r['presentation']['artifacts'] = [
                {'label': 'planned result', 'stepId': 'A', 'state': 'PLANNED', 'path': value}]
            self.assertEqual(WorkflowPublisherTests.publisher.project(r)['intake']['status'], 'READY')
            self.assertIn(board.text(value), board.render(r))

    def test_resolved_data_gap_requires_new_evidence(self):
        r=self.record();r['intake']['dataNeeds']=[{'id':'X','label':'boundary','required':True,'reason':'extent','recommendation':'provide polygon','state':'VERIFIED','stepIds':['A']}]
        with self.assertRaises(ValueError):board.validate(r)


if __name__ == '__main__':
    unittest.main(verbosity=2)
