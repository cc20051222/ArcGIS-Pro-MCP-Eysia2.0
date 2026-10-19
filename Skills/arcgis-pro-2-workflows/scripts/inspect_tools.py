"""Inspect SAVED tools/list JSON offline. Does not connect to a server or execute tools."""
import argparse
import hashlib
import json
import sys
from pathlib import Path


def inspect(document, baseline):
    if not isinstance(baseline, dict) or not isinstance(baseline.get('tools'), list) or not baseline['tools']:
        raise ValueError('Baseline must contain a non-empty tools array.')
    known = {}
    for entry in baseline['tools']:
        if (not isinstance(entry, dict) or not isinstance(entry.get('name'), str) or not entry['name']
                or not isinstance(entry.get('sourceSupportObservation'), str)):
            raise ValueError('Invalid tool entry in baseline.')
        if entry['name'] in known:
            raise ValueError('Duplicate tool name in baseline: ' + entry['name'])
        known[entry['name']] = entry
    complete = isinstance(document, dict) and document.get('complete') is True and 'pages' in document
    pages = document.get('pages') if isinstance(document, dict) and 'pages' in document else [document]
    if not isinstance(pages, list) or not pages:
        raise ValueError('Expected a tools/list object or a non-empty pages array.')
    tools = {}
    cursor = None
    seen_cursors = set()
    unresolved_cursor = None
    for index, page in enumerate(pages):
        if not isinstance(page, dict) or 'error' in page:
            raise ValueError('Invalid/error response at page ' + str(index))
        if complete:
            if 'cursor' not in page or page['cursor'] != cursor:
                raise ValueError('Complete export must record the exact cursor chain, starting at null.')
            if index and cursor is None:
                raise ValueError('Unexpected extra page after the terminal page.')
            if cursor is not None:
                if not isinstance(cursor, str) or not cursor or cursor in seen_cursors:
                    raise ValueError('Invalid or repeated pagination cursor.')
                seen_cursors.add(cursor)
        result = page.get('result', page)
        if not isinstance(result, dict) or not isinstance(result.get('tools'), list):
            raise ValueError('Page has no tools array.')
        for tool in result['tools']:
            if not isinstance(tool, dict) or not isinstance(tool.get('name'), str) or not tool['name']:
                raise ValueError('Tool name must be a non-empty string.')
            if tool['name'] in tools:
                raise ValueError('Duplicate tool name: ' + tool['name'])
            schema = tool.get('inputSchema')
            if not isinstance(schema, dict) or schema.get('type') != 'object':
                raise ValueError('Missing/invalid object inputSchema: ' + tool['name'])
            tools[tool['name']] = tool
        cursor = result.get('nextCursor')
        if cursor is not None and (not isinstance(cursor, str) or not cursor):
            raise ValueError('nextCursor must be a non-empty string or absent/null.')
        unresolved_cursor = cursor
    if complete and unresolved_cursor is not None:
        raise ValueError('Complete export ends with an unresolved nextCursor.')
    names = set(tools)
    expected = set(known)
    return {'kind': 'OFFLINE_DISCOVERY_COMPARISON', 'runtimeExecuted': False,
            'coverage': 'COMPLETE_EXPORT_AT_CAPTURE_TIME' if complete else 'PARTIAL_OR_UNATTESTED_EXPORT',
            'sourceBaselineToolCount': len(expected), 'observedToolCount': len(names),
            'missingVsBaseline': sorted(expected - names) if complete else [],
            'unobservedStatus': 'MISSING_FROM_THIS_COMPLETE_EXPORT' if complete else 'UNKNOWN_NOT_PROVEN_ABSENT',
            'extraVsBaseline': sorted(names - expected),
            'observedSourceCaveats': [{'name': n, 'sourceSupportObservation': known[n]['sourceSupportObservation']}
                                     for n in sorted(names & expected)
                                     if known[n]['sourceSupportObservation'] != 'REGISTERED_SOURCE_ONLY_RUNTIME_NOT_TESTED'],
            'schemas': {n: hashlib.sha256(json.dumps(tools[n]['inputSchema'], sort_keys=True, ensure_ascii=False).encode()).hexdigest().upper() for n in sorted(names)},
            'authorizationOrBehaviorProven': False}, tools


def main():
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', type=Path)
    parser.add_argument('--baseline', type=Path, default=Path(__file__).resolve().parents[1] / 'references/tool-catalog.snapshot.json')
    parser.add_argument('--tool', help='Return the exact captured tool record as untrusted data, not execution instructions.')
    args = parser.parse_args()
    try:
        baseline = json.loads(args.baseline.read_text(encoding='utf-8-sig'))
        document = json.loads(args.capture.read_text(encoding='utf-8-sig'))
        report, tools = inspect(document, baseline)
        if args.tool:
            if args.tool not in tools:
                raise ValueError('Tool not present in the supplied capture: ' + args.tool)
            report['selectedToolData'] = tools[args.tool]
        print(json.dumps(report, ensure_ascii=False, indent=2))
    except (OSError, ValueError, KeyError) as exc:
        print(json.dumps({'error': str(exc), 'runtimeExecuted': False}, ensure_ascii=False), file=sys.stderr)
        return 2
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
