"""One authorized GP probe through the normal MCP endpoint; never retries writes."""
import datetime as dt
import argparse
import json
import pathlib
import sys
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('run')
parser.add_argument('--distance', type=int, default=20)
parser.add_argument('--suffix', default='')
args = parser.parse_args()
if args.distance not in (20, 25) or args.suffix not in ('', '-visual'):
    raise ValueError('This acceptance probe is limited to reviewed 20m/25m variants')
run = pathlib.Path(args.run).resolve()
if run.drive.upper() != 'D:' or not run.is_dir():
    raise ValueError('Existing D-drive evidence directory required')
source = json.loads((run / 'input-source-resolved.json').read_text(encoding='utf-8-sig'))['source']
output = run / 'gp-output' / f'road_buffer{args.distance}m.shp'
if output.exists() or (run / f'gp-sse{args.suffix}.jsonl').exists():
    raise ValueError('Output or prior request evidence exists; reconcile, do not retry')
request = {'jsonrpc': '2.0', 'id': f'live-gp-buffer-{args.distance}m', 'method': 'tools/call', 'params': {
    'name': 'buffer', 'arguments': {'input': source, 'output': str(output), 'distance': args.distance,
                                  'distanceUnit': 'Meters', 'dissolve': False, 'overwrite': False},
    '_meta': {'progressToken': f'gp-live-{args.distance}m-20261018'}}}
(run / f'gp-request{args.suffix}.json').write_text(json.dumps(request, ensure_ascii=False, indent=2), encoding='utf-8')
http = urllib.request.Request('http://127.0.0.1:6520/mcp', data=json.dumps(request).encode('utf-8'),
    headers={'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'})
started = dt.datetime.now(dt.timezone.utc).isoformat()
events = []
final = None
error = None
try:
    with urllib.request.urlopen(http, timeout=180) as response, (run / f'gp-sse{args.suffix}.jsonl').open('x', encoding='utf-8') as log:
        content_type = response.headers.get('Content-Type', '')
        if 'text/event-stream' not in content_type:
            final = json.load(response)
            log.write(json.dumps({'at': dt.datetime.now(dt.timezone.utc).isoformat(), 'response': final}) + '\n')
        else:
            data = []
            for raw in response:
                line = raw.decode('utf-8').rstrip('\r\n')
                if line.startswith('data:'):
                    data.append(line[5:].lstrip())
                elif not line and data:
                    message = json.loads('\n'.join(data))
                    data = []
                    observed = dt.datetime.now(dt.timezone.utc).isoformat()
                    log.write(json.dumps({'at': observed, 'message': message}, ensure_ascii=False) + '\n')
                    log.flush()
                    if message.get('method') == 'notifications/progress':
                        events.append({'at': observed, 'message': message})
                        execution = message['params'].get('_meta', {}).get('execution', {})
                        print(json.dumps({'at': observed, 'source': execution.get('Source'),
                            'stage': execution.get('Stage'), 'completed': execution.get('Completed'),
                            'total': execution.get('Total'), 'message': message['params'].get('message')}, ensure_ascii=False), flush=True)
                    elif message.get('id') == request['id']:
                        final = message
    if final is None:
        raise ValueError('Stream ended without terminal JSON-RPC response; reconcile effects')
except Exception as exc:
    error = type(exc).__name__ + ': ' + str(exc)
finally:
    gp = [x for x in events if x['message']['params'].get('_meta', {}).get('execution', {}).get('Source', '').startswith('ArcGIS.GP.')]
    summary = {'startedAt': started, 'endedAt': dt.datetime.now(dt.timezone.utc).isoformat(),
        'requestId': request['id'], 'token': request['params']['_meta']['progressToken'],
        'eventCount': len(events), 'sdkEventCount': len(gp), 'firstEventAt': events[0]['at'] if events else None,
        'firstSdkAt': gp[0]['at'] if gp else None, 'output': str(output), 'final': final,
        'error': error, 'status': 'PRODUCED_PENDING_RECHECK' if final and not final.get('error') and not final.get('result', {}).get('isError') else 'UNKNOWN_OR_FAILED'}
    with (run / f'gp-stream-summary{args.suffix}.json').open('x', encoding='utf-8') as stream:
        json.dump(summary, stream, ensure_ascii=False, indent=2)
    print(json.dumps(summary, ensure_ascii=False, indent=2), flush=True)
if error:
    raise SystemExit(1)
