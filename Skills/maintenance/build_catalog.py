"""Build an offline skill reference from the current 2.0 source. Never calls GIS."""
import argparse
import collections
import hashlib
import json
import re
from pathlib import Path


# Reviewed 2026-10-18. A changed file invalidates the support observation, even
# when names still match: regeneration must not silently reuse old conclusions.
REVIEWED_SUPPORT_SOURCES = {
    'D091PsTools.cs': '60B705C7C43FCD13BC5EF42CA2EA4CB4304C005E86E1CF85AC5031290D4B1BE1',
    'D102PsTools.cs': 'AA93DB75D36505351A24C4E360983522064FDC9E0D96161EC82BBE40F49859CA',
    'D118HostFaceTools.cs': '5C0601706A085DC992F77A02AF636A276AE1AAB8A97357096366090D10005320',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def build(repo, date):
    skill_header = (repo / 'Skills/arcgis-pro-2-workflows/SKILL.md').read_text(encoding='utf-8-sig').split('---', 2)[1]
    version = re.search(r'^  version: "([0-9]+\.[0-9]+\.[0-9]+)"\s*$', skill_header, re.M)
    if not version:
        raise ValueError('Cannot resolve the current Skill version from frontmatter.')
    snapshot = repo / 'Tests/UnitTests/ProductionToolContractSnapshot.cs'
    composition = repo / 'Source/ArcGISProMCP.Compatibility/Composition.cs'
    classification = repo / 'Source/Shared/ArcGISProMCP.Core/Services/IReadOnlyModeService.cs'
    errors = repo / 'Source/Shared/ArcGISProMCP.Core/Results/ErrorCodes.cs'
    whitelist = repo / 'Config/gp-whitelist.json'
    text = snapshot.read_text(encoding='utf-8-sig')
    rows = re.findall(r'new\("([a-z0-9_]+)",\s*ToolCategories\.(\w+),\s*ExecutionTypes\.(\w+),\s*(true|false)\)', text)
    registered = re.findall(r'registry\.Register\(new\s+(\w+)\s*\(', composition.read_text(encoding='utf-8-sig'))
    if not rows or len(rows) != len(registered) or len(set(registered)) != len(registered):
        raise ValueError('Registration/contract count mismatch or duplicate registered classes.')
    if len({r[0] for r in rows}) != len(rows):
        raise ValueError('Duplicate contract names.')
    class_index = {}
    constants = {}
    files = sorted((repo / 'Source/Shared/ArcGISProMCP.Tools').glob('*.cs'))
    for file in files:
        body = file.read_text(encoding='utf-8-sig')
        constants.update(re.findall(r'public const string\s+(\w+)\s*=\s*"([a-z0-9_]+)"', body))
        matches = list(re.finditer(r'(?m)^\s*(?:(?:public|internal|abstract|sealed|static|partial)\s+)*class\s+(\w+)(?:\s*:\s*([^\n{]+))?', body))
        for i, match in enumerate(matches):
            name = match[1]
            if name in class_index:
                raise ValueError('Ambiguous class: ' + name)
            block = body[match.end():matches[i+1].start() if i+1 < len(matches) else len(body)]
            class_index[name] = (file, match[2], block)

    def name_for(cls):
        file, parent, block = class_index[cls]
        expression = re.search(r'public\s+(?:override\s+)?string\s+Name\s*=>\s*([^;]+);', block)
        if not expression:
            raise ValueError('Cannot statically resolve name for ' + cls)
        expression = expression[1].strip()
        literal = re.fullmatch(r'"([a-z0-9_]+)"', expression)
        if literal:
            return literal[1]
        if expression == 'ToolKey':
            key = re.search(r'public const string ToolKey\s*=\s*D118FrozenSchemas\.(\w+)\s*;', block)
            if key and key[1] in constants:
                return constants[key[1]]
        raise ValueError('Unsupported name expression in ' + cls + ': ' + expression)

    by_name = {}
    for cls in registered:
        name = name_for(cls)
        if name in by_name:
            raise ValueError('Duplicate source tool name: ' + name)
        by_name[name] = cls
    if set(by_name) != {r[0] for r in rows}:
        raise ValueError('Registered source names differ from contract snapshot names.')
    tier_text = classification.read_text(encoding='utf-8-sig')
    tiers = {}
    for label in ('Read', 'Session', 'Write'):
        match = re.search(label + r'Tools\s*=.*?\{(.*?)\n    \};', tier_text, re.S)
        if not match:
            raise ValueError('Cannot locate classification set ' + label)
        clean = re.sub(r'//[^\n]*', '', match[1])
        for name in re.findall(r'"([a-z0-9_]+)"', clean):
            if name in tiers:
                raise ValueError('Duplicate classification name: ' + name)
            tiers[name] = label
    error_codes = re.findall(r'public const string\s+\w+\s*=\s*"([A-Z_]+)"', errors.read_text(encoding='utf-8-sig'))
    gp = json.loads(whitelist.read_text(encoding='utf-8-sig'))
    if len({r['tool'] for r in gp}) != len(gp):
        raise ValueError('Duplicate GP allowlist name.')
    host_closed = {'export_time_animation', 'extrude_scene_features', 'create_elevation_profile', 'export_scene_package'}
    tools = []
    for name, category, execution, requires in sorted(rows):
        cls = by_name[name]
        source_file = class_index[cls][0]
        status = 'REGISTERED_SOURCE_ONLY_RUNTIME_NOT_TESTED'
        if name.startswith('ps_'):
            if sha(source_file) != REVIEWED_SUPPORT_SOURCES.get(source_file.name):
                status = 'SOURCE_CHANGED_SUPPORT_REVIEW_REQUIRED'
            else:
                status = 'DIAGNOSTIC_ONLY_PEER_NOT_VERIFIED' if name == 'ps_get_capabilities' else 'SOURCE_FAIL_CLOSED_PS_UXP_NOT_IMPLEMENTED'
        elif name in host_closed:
            status = ('SOURCE_FAIL_CLOSED_HOST_FACE_NOT_IMPLEMENTED'
                      if sha(source_file) == REVIEWED_SUPPORT_SOURCES.get(source_file.name)
                      else 'SOURCE_CHANGED_SUPPORT_REVIEW_REQUIRED')
        tools.append({'name': name, 'category': category, 'executionType': execution,
                      'requiresArcGISMetadata': requires == 'true', 'sourceClass': cls,
                      'sourcePath': class_index[cls][0].relative_to(repo).as_posix(),
                      'sourceTier': tiers.get(name, 'WriteDefaultUnknown'),
                      'sourceSupportObservation': status})
    observed_sources = {snapshot, composition, classification, errors, whitelist}
    observed_sources.update(class_index[c][0] for c in registered)
    return {'kind': 'OFFLINE_SOURCE_TOOL_REFERENCE', 'generatedOn': date,
            'product': 'ArcGIS-Pro-MCP 2.0', 'skillVersion': version[1],
            'evidenceStatus': 'SOURCE_INSPECTED_NOT_RUNTIME_ACCEPTANCE',
            'registeredToolCount': len(tools), 'registrationClassCount': len(registered),
            'sourceContractNameDifference': [],
            'supportObservationReviewDate': '2026-10-18',
            'supportObservationBoundary': 'Reviewed limitations apply only to the exact reviewed source hashes. Changed PS/host files require a new review, not automatic promotion.',
            'executionTypeCounts': dict(sorted(collections.Counter(t['executionType'] for t in tools).items())),
            'rosterCountsIncludingUnregisteredCandidates': dict(collections.Counter(tiers.values())),
            'tierBoundary': 'Source set membership only; not a new behavioral read/write acceptance. Read tier includes control operations such as set_readonly_mode.',
            'tools': tools, 'errorCodes': error_codes,
            'gpWhitelist': {'count': len(gp), 'sourceSha256': sha(whitelist),
                            'entries': [{'tool': r['tool'], 'destructive': r['destructive'],
                                         'parameterNames': [p['name'] for p in r['parameters']]} for r in gp],
                            'boundary': 'Allowlisted does not prove a successful licensed call; no expansion authority.'},
            'sources': [{'path': p.relative_to(repo).as_posix(), 'sha256': sha(p)} for p in sorted(observed_sources)]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo-root', type=Path, required=True)
    parser.add_argument('--date', required=True, help='Review date in YYYY-MM-DD, not a live-runtime timestamp.')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--replace', action='store_true')
    args = parser.parse_args()
    output = args.output.resolve()
    if output.drive.upper() != 'D:':
        parser.error('Project output must be on D: under the current workspace policy.')
    if output.exists() and not args.replace:
        parser.error('Output exists. Review the candidate before using --replace.')
    try:
        result = build(args.repo_root.resolve(), args.date)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    except (OSError, ValueError, KeyError) as exc:
        parser.exit(2, str(exc) + '\n')
    print(json.dumps({k: result[k] for k in ('registeredToolCount', 'executionTypeCounts', 'evidenceStatus')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
