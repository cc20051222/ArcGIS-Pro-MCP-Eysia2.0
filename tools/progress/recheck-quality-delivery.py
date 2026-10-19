"""Read-only final reconciliation of actual GP, SDK frame interval and quality outputs."""
import datetime as dt, hashlib, json, pathlib, struct, sys, urllib.request, zipfile

run=pathlib.Path(sys.argv[1]).resolve()
if run.drive.upper()!='D:':raise ValueError('D output required')
out=run/('final-recheck-'+dt.datetime.now().strftime('%H%M%S%f'));out.mkdir()
checks=[]
def record(name,ok,**facts):
    checks.append({'name':name,'result':'PASS' if ok else 'FAIL',**facts})
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def stamp(s):return dt.datetime.fromisoformat(s)
def call(name,args,tag):
    q={'jsonrpc':'2.0','id':'final-'+tag,'method':'tools/call','params':{'name':name,'arguments':args}}
    req=urllib.request.Request('http://127.0.0.1:6520/mcp',data=json.dumps(q).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=180) as r:value=json.load(r)
    (out/(tag+'.json')).write_text(json.dumps({'request':q,'response':value},ensure_ascii=False,indent=2),encoding='utf-8')
    if value.get('error') or value.get('result',{}).get('isError'):raise ValueError(value)
    return json.loads(next(c['text'] for c in value['result']['content'] if c.get('type')=='text'))

fixture=read(run/'fixtures.json')
record('all fixture members unchanged',all(hashlib.sha256(pathlib.Path(x['path']).read_bytes()).hexdigest().upper()==x['sha256'] for x in fixture['manifest']),files=len(fixture['manifest']))
stream=[json.loads(x) for x in (run/'progress-sse.jsonl').read_text(encoding='utf-8').splitlines()]
events=[x for x in stream if x['message'].get('method')=='notifications/progress']
sdk=[x for x in events if x['message']['params']['_meta']['execution']['Source'].startswith('ArcGIS.GP.')]
finals=[x for x in stream if x['message'].get('id')=='quality-fix-live-buffer']
record('real terminal response success',len(finals)==1 and not finals[0]['message'].get('error') and isinstance(finals[0]['message'].get('result'),dict) and not finals[0]['message']['result'].get('isError'))
record('progress token, request and increasing event sequence',bool(sdk) and all(x['message']['params']['progressToken']=='quality-fix-live-token' and x['message']['params']['_meta']['execution']['RequestId']=='quality-fix-live-buffer' for x in events) and all(a['message']['params']['progress']<b['message']['params']['progress'] for a,b in zip(events,events[1:])),events=len(events),sdkEvents=len(sdk))
begin=min(stamp(x['message']['params']['_meta']['execution']['ObservedAt']) for x in sdk)
ends=[stamp(x['message']['params']['_meta']['execution']['ObservedAt']) for x in sdk if x['message']['params']['_meta']['execution']['Source']=='ArcGIS.GP.OnEndExecute']
end=min(ends) if ends else min(stamp(x['at']) for x in finals)
frames=[]
for row in [json.loads(x) for x in (run/'running-panel-observations.jsonl').read_text(encoding='utf-8-sig').splitlines()]:
    if not row.get('screenshot'):continue
    text=' '.join(row['text'])
    valid=begin<=stamp(row['captureStartedAt'])<=stamp(row['captureEndedAt'])<end and '执行中' in row['text'] and 'quality-fix-live-buffer' in text and 'ArcGIS.GP.' in text and 'OnEndExecute' not in text and pathlib.Path(row['screenshot']).is_file()
    frames.append({'path':row['screenshot'],'verifiedDuringSdkExecution':valid,'startedAt':row['captureStartedAt'],'endedAt':row['captureEndedAt'],'sha256':hashlib.sha256(pathlib.Path(row['screenshot']).read_bytes()).hexdigest().upper()})
record('actual native running frames inside SDK execution interval',bool(frames) and all(f['verifiedDuringSdkExecution'] for f in frames),sdkBegin=begin.isoformat(),sdkEnd=end.isoformat(),frames=frames)

original=pathlib.Path(r'D:\shiyanwenjaijia\自测记录\进度面板实测\20261018-173734-fd93ca32\gp-output')
for tag,path,expected in [('buffer20m',original/'road_buffer20m.shp',6),('buffer25m',original/'road_buffer25m.shp',6),('stress3m',run/'progress-buffer.shp',10000)]:
    # First read dataset tolerance; use it as the documented native numerical threshold,
    # not a fabricated business accuracy requirement.
    initial=call('validate_geometries',{'dataset':str(path),'checks':['null_geometry'],'maxFeatures':1000000},tag+'-precheck')
    tolerance=initial['xyTolerance']
    if not isinstance(tolerance,(int,float)) or tolerance<=0:raise ValueError('Unknown positive dataset XYTolerance')
    geometry=call('validate_geometries',{'dataset':str(path),'minimumSegmentLength':tolerance,'maxFeatures':1000000},tag+'-geometry')
    rules=['must_not_self_intersect']+(['must_not_overlap'] if tag=='stress3m' else [])
    topology=call('check_topology_rules',{'dataset':str(path),'rules':rules},tag+'-topology')
    record(tag+' full geometry checks',geometry['qualityVerdict']=='PASS' and geometry['coverageComplete'] and geometry['allRequestedChecksApplied'] and geometry['scannedCount']==expected,total=geometry['totalFeatureCount'],scanned=geometry['scannedCount'],issueCount=geometry['issueCount'],verdict=geometry['qualityVerdict'],minimumSegmentLength=tolerance,thresholdBasis='dataset XYTolerance, not business accuracy certification')
    record(tag+' full applicable topology',topology['qualityVerdict']=='PASS' and topology['coverageComplete'] and topology['scannedCount']==expected,rules=rules,verdict=topology['qualityVerdict'],issueCount=topology['issueCount'],note='Individual road buffers legitimately overlap; no rule is invented to forbid that. No unspecified coverage boundary is certified.')
    # Independent file-header record census and physical member checks.
    raw=path.read_bytes();offset=100;count=0;nulls=0
    while offset<len(raw):
        _,words=struct.unpack_from('>ii',raw,offset);size=words*2
        if size<4 or offset+8+size>len(raw):raise ValueError('Invalid SHP record length')
        kind=struct.unpack_from('<i',raw,offset+8)[0];nulls+=kind==0;count+=1;offset+=8+size
    dbf=path.with_suffix('.dbf').read_bytes();dbf_count=struct.unpack_from('<I',dbf,4)[0]
    record(tag+' independent SHP and DBF census',offset==len(raw) and count==expected and dbf_count==expected and nulls==0 and all(path.with_suffix(s).is_file() for s in ['.shp','.shx','.dbf','.prj']),shpCount=count,dbfCount=dbf_count,nulls=nulls)

env=call('get_environment',{},'environment-final')
before=next(run.glob('get_environment-*.json'))
record('GP session environment restored',env==json.loads(read(before)['result']['content'][0]['text']))
summary={'at':dt.datetime.now().astimezone().isoformat(),'status':'PASS CANDIDATE' if all(c['result']=='PASS' for c in checks) else 'CHANGES REQUIRED','passed':sum(c['result']=='PASS' for c in checks),'total':len(checks),'checks':checks,'uiObserverNote':'The capture observer raised ElementNotAvailableException after six frames; the GP request completed once. This reconciliation uses retained candidates against SDK ObservedAt, not a replay.','independentAcceptance':'PENDING'}
(out/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':summary['status'],'passed':summary['passed'],'total':summary['total'],'evidence':str(out/'summary.json')},ensure_ascii=False),flush=True)
if summary['passed']!=summary['total']:raise SystemExit(1)
