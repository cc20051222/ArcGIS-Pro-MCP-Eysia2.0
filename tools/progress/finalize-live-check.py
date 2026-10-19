"""Read back live GP and file evidence; does not execute GIS or approve acceptance."""
import datetime as dt
import decimal
import hashlib
import json
import pathlib
import struct
import sys

run = pathlib.Path(sys.argv[1]).resolve()
def read(name):
    return json.loads((run / name).read_text(encoding='utf-8-sig'))
def body(name):
    value = read(name)
    if value.get('error') or value.get('result', {}).get('isError'):
        raise ValueError(f'Tool did not succeed: {name}')
    return json.loads(value['result']['content'][0]['text'])
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def dbf_rows(path):
    data = path.read_bytes()
    count, header_length, record_length = struct.unpack_from('<IHH', data, 4)
    fields = []
    offset = 1
    for pos in range(32, header_length - 1, 32):
        if data[pos] == 13:
            break
        name = data[pos:pos + 11].split(b'\0')[0].decode('ascii')
        width = data[pos + 16]
        fields.append((name, offset, width))
        offset += width
    rows = []
    for index in range(count):
        record = data[header_length + index * record_length:header_length + (index + 1) * record_length]
        if len(record) != record_length or record[:1] == b'*':
            raise ValueError('Short or deleted DBF row')
        rows.append({name: record[start:start+width].decode('ascii').strip() for name, start, width in fields})
    return rows

checks = []
source = pathlib.Path(read('input-source-resolved.json')['source'])
before = read('input-before-manifest.json')
unchanged = all(digest(pathlib.Path(x['path'])) == x['sha256'] for x in before)
checks.append({'criterion': 'original road members unchanged', 'pass': unchanged, 'checked': len(before)})
input_box = struct.unpack_from('<dddd', source.read_bytes(), 36)
for distance, suffix in ((20, ''), (25, '-visual')):
    s = read(f'gp-stream-summary{suffix}.json')
    messages = [json.loads(x) for x in (run / f'gp-sse{suffix}.jsonl').read_text(encoding='utf-8').splitlines()]
    progress = [x for x in messages if x['message'].get('method') == 'notifications/progress']
    final = [x for x in messages if x['message'].get('id') == s['requestId']]
    tokens_ok = all(x['message']['params']['progressToken'] == s['token'] for x in progress)
    counters = [x['message']['params']['progress'] for x in progress]
    monotonic = all(a < b for a, b in zip(counters, counters[1:]))
    sdk = [x for x in progress if x['message']['params']['_meta']['execution']['Source'].startswith('ArcGIS.GP.')]
    stream_ok = bool(len(final) == 1 and sdk and tokens_ok and monotonic
        and all('total' not in x['message']['params'] for x in progress)
        and all(x['at'] <= final[0]['at'] for x in progress)
        and not final[0]['message'].get('error') and not final[0]['message']['result'].get('isError'))
    checks.append({'criterion': f'{distance}m actual SDK stream before terminal result', 'pass': stream_ok,
                   'progressEvents': len(progress), 'sdkEvents': len(sdk), 'counterUnit': 'observed event count'})
    output = pathlib.Path(s['output'])
    geometry = body(f'qc-{distance}-get_geometry_info.json')
    rows = dbf_rows(output.with_suffix('.dbf'))
    bbox = struct.unpack_from('<dddd', output.read_bytes(), 36)
    expected = (input_box[0] - distance, input_box[1] - distance, input_box[2] + distance, input_box[3] + distance)
    bbox_deltas = [actual - target for actual, target in zip(bbox, expected)]
    # Numerical read-back comparison only, not a surveying accuracy claim.
    numeric_ok = all(abs(x) <= 1e-5 for x in bbox_deltas)
    fields_ok = len(rows) == 6 and all(decimal.Decimal(x['BUFF_DIST']) == distance for x in rows) and len({x['ORIG_FID'] for x in rows}) == 6
    geometry_ok = geometry['totalFeatureCount'] == geometry['scannedCount'] == 6 and not geometry['truncated'] and geometry['emptyGeometryCount'] == 0 and geometry['geometryType'] == 'Polygon' and geometry['coordinateUnit'] == 'Meter'
    checks.append({'criterion': f'{distance}m data recheck via SDK + independent DBF/SHP reader',
        'pass': numeric_ok and fields_ok and geometry_ok, 'count': len(rows), 'bufferDistance': distance,
        'allAttributesMatch': fields_ok, 'bboxDifferenceMeters': bbox_deltas,
        'numericComparisonToleranceMeters': 1e-5, 'topologyClaim': 'NOT VERIFIED'})
    required = ['.shp', '.shx', '.dbf', '.prj']
    checks.append({'criterion': f'{distance}m required shapefile members', 'pass': all(output.with_suffix(ext).is_file() for ext in required)})
manifest = []
locks = []
for path in sorted((run / 'gp-output').iterdir()):
    if not path.is_file():
        continue
    if path.name.endswith('.lock'):
        locks.append(str(path))
        continue
    manifest.append({'path': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)})
report = {'observedAt': dt.datetime.now().astimezone().isoformat(),
    'status': 'PASS CANDIDATE' if all(x['pass'] for x in checks) else 'CHANGES REQUIRED',
    'scope': 'progress feature and bounded GP read-back; not all 239 tools or full topology acceptance',
    'checks': checks, 'outputManifest': manifest, 'excludedLiveLocks': locks,
    'limitations': ['Existing validate_geometries returned NOT_IMPLEMENTED; full topology checks not verified.',
                    'UI screenshots show actual retained GP history; in-flight GP screenshots were not captured.',
                    'External final acceptance remains pending.'],
    'acceptance': {'state': 'PENDING', 'producerApprovedItself': False}}
(run / 'final-recheck.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
if not all(x['pass'] for x in checks):
    raise SystemExit(1)
