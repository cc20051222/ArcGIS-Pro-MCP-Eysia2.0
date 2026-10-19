"""Real MCP black-box checks against explicit synthetic fixtures; no GIS code execution."""
import datetime as dt
import hashlib
import json
import pathlib
import sys
import urllib.request

run = pathlib.Path(sys.argv[1]).resolve()
if run.drive.upper() != 'D:':
    raise ValueError('D root required')
fixture = json.loads((run/'fixtures.json').read_text(encoding='utf-8-sig'))
paths = fixture['paths']
out = run / ('quality-checks-' + dt.datetime.now().strftime('%H%M%S%f'))
out.mkdir(exist_ok=False)
results = []

def call(index, tool, arguments):
    request = {'jsonrpc':'2.0','id':'quality-'+str(index),'method':'tools/call','params':{'name':tool,'arguments':arguments}}
    r = urllib.request.Request('http://127.0.0.1:6520/mcp',data=json.dumps(request).encode('utf-8'),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(r,timeout=120) as response:
        value = json.load(response)
    (out/(f'{index:02d}-{tool}.json')).write_text(json.dumps({'request':request,'response':value},ensure_ascii=False,indent=2),encoding='utf-8')
    error = value.get('error') or value.get('result',{}).get('isError')
    text = next((c['text'] for c in value.get('result',{}).get('content',[]) if c.get('type')=='text'),'')
    if error:
        return None, text or str(error)
    return json.loads(text), None

def check(name,tool,arguments,expect,extra=lambda data:True):
    index=len(results)+1
    try:
        value,error=call(index,tool,arguments)
        if expect.startswith('ERROR:'):
            ok=bool(error and expect[6:] in error)
        else:
            ok=not error and value.get('qualityVerdict')==expect and extra(value)
        item={'index':index,'name':name,'result':'PASS' if ok else 'FAIL','expected':expect,
              'actual':value.get('qualityVerdict') if value else None,'rpcError':error,
              'issueCount':value.get('issueCount') if value else None}
    except Exception as e:
        item={'index':index,'name':name,'result':'FAIL','error':type(e).__name__+': '+str(e)}
    results.append(item)
    print(json.dumps(item,ensure_ascii=False),flush=True)

def g(name,**extra): return {'dataset':paths[name],'minimumSegmentLength':0.01,**extra}
def t(name,*rules,**extra): return {'dataset':paths[name],'rules':list(rules),**extra}
def count(kind,expected):
    return lambda data: any(c['check']==kind and c['issueCount']>=expected for c in data['checks'])

check('valid adjacent polygons; all four checks applied','validate_geometries',g('adjacent'),'PASS',lambda d:d['coverageComplete'] and d['allRequestedChecksApplied'] and d['scannedCount']==2)
check('bow-tie rejected by forced OGC validation','validate_geometries',g('bowtie'),'FAIL',lambda d:d['issueCount']>=1)
check('duplicate vertex detected; required closure excluded','validate_geometries',g('duplicate_vertex'),'FAIL',count('duplicate_vertex',1))
check('short line segment detected in CRS units','validate_geometries',g('short_segment'),'FAIL',count('short_segment',1))
check('duplicate multipoint detected','validate_geometries',g('multipoint'),'FAIL',count('duplicate_vertex',1))
check('multipoint segment-only check not applicable','validate_geometries',g('multipoint',checks=['short_segment']),'PASS',lambda d:d['checks'][0]['status']=='NOT_APPLICABLE')
check('legal line self-touch not a self-intersection','validate_geometries',g('self_touch',checks=['self_intersection']),'PASS')
check('legal line self-touch allowed by topology','check_topology_rules',t('self_touch','must_not_self_intersect'),'PASS')
check('line interior crossing detected','check_topology_rules',t('self_cross','must_not_self_intersect'),'FAIL')
check('crossing exactly at repeated vertex detected','check_topology_rules',t('vertex_cross','must_not_self_intersect'),'FAIL')
check('vertex crossing other segment interior detected','check_topology_rules',t('vertex_segment_cross','must_not_self_intersect'),'FAIL')
check('zero-length node neighbors cannot hide crossing','check_topology_rules',t('duplicate_node_cross','must_not_self_intersect'),'FAIL')
check('short segment alone not a self-intersection','check_topology_rules',t('short_segment','must_not_self_intersect'),'PASS')
check('null shape detected','validate_geometries',g('null_geometry'),'FAIL',count('null_geometry',1))
check('sample cannot be full-check PASS','validate_geometries',g('adjacent',maxFeatures=1),'INCOMPLETE',lambda d:d['truncated'] and not d['coverageComplete'])
check('empty dataset cannot be ordinary PASS','validate_geometries',g('empty'),'NO_FEATURES')
check('zero short-segment threshold explicitly not applied','validate_geometries',g('adjacent',minimumSegmentLength=0),'PASS_WITH_LIMITATIONS',lambda d:not d['allRequestedChecksApplied'])
check('adjacent boundaries allowed by overlap check','check_topology_rules',t('adjacent','must_not_overlap','must_not_have_gaps','must_not_self_intersect','must_be_single_part'),'PASS')
check('partial interior overlap detected','check_topology_rules',t('overlap','must_not_overlap'),'FAIL',lambda d:d['issueCount']==1)
check('containment counts as overlap','check_topology_rules',t('containment','must_not_overlap'),'FAIL',lambda d:d['issueCount']==1)
check('identical polygons count as overlap','check_topology_rules',t('duplicate_polygons','must_not_overlap'),'FAIL',lambda d:d['issueCount']==1)
check('enclosed gap detected in union','check_topology_rules',t('gap','must_not_have_gaps','must_not_overlap'),'FAIL',lambda d:d['issueCount']==1)
check('polygon hole is not a second exterior part','check_topology_rules',t('donut','must_be_single_part','must_not_self_intersect'),'PASS')
check('polygon hole detected when no-gap rule selected','check_topology_rules',t('donut','must_not_have_gaps'),'FAIL',lambda d:d['issueCount']==1)
check('two exterior polygon parts detected','check_topology_rules',t('multipart','must_be_single_part'),'FAIL')
check('line crossing at point is not line overlap','check_topology_rules',t('crossing_lines','must_not_overlap'),'PASS')
check('line segment self overlap detected','check_topology_rules',t('self_overlap','must_not_self_overlap'),'FAIL')
check('closed path has no dangling endpoints','check_topology_rules',t('closed_line','must_not_have_dangles'),'PASS')
check('connected open network has exactly two dangles','check_topology_rules',t('open_lines','must_not_have_dangles'),'FAIL',lambda d:d['issueCount']==2)
check('unconnected near endpoints retain four dangles at default','check_topology_rules',t('near_endpoints','must_not_have_dangles'),'FAIL',lambda d:d['issueCount']==4)
check('explicit connectivity distance recognizes near connection','check_topology_rules',t('near_endpoints','must_not_have_dangles',clusterTolerance=0.1),'FAIL',lambda d:d['issueCount']==2)
check('unknown rule refused','check_topology_rules',t('adjacent','must_be_magic'),'ERROR:INVALID_ARGUMENT')
check('unsupported clustering not silently ignored','check_topology_rules',t('adjacent','must_not_overlap',clusterTolerance=0.1),'ERROR:INVALID_ARGUMENT')
report=out/'native-quality-report.json'
check('native report written through guarded tool','validate_geometries',g('adjacent',reportPath=str(report)),'PASS',lambda d:report.is_file() and 'artifactManifest' in d)
check('native report overwrite refused','validate_geometries',g('adjacent',reportPath=str(report)),'ERROR:OUTPUT_EXISTS')

unchanged=all(hashlib.sha256(pathlib.Path(f['path']).read_bytes()).hexdigest().upper()==f['sha256'] for f in fixture['manifest'])
results.append({'index':len(results)+1,'name':'all synthetic input files unchanged','result':'PASS' if unchanged else 'FAIL','files':len(fixture['manifest'])})
summary={'observedAt':dt.datetime.now().astimezone().isoformat(),'status':'PASS CANDIDATE' if all(r['result']=='PASS' for r in results) else 'CHANGES REQUIRED',
         'total':len(results),'passed':sum(r['result']=='PASS' for r in results),'cases':results,'evidence':str(out),
         'scope':'Explicit six-rule implementation and geometry contract; not an assertion that every Esri topology rule or industry standard is implemented.',
         'independentAcceptance':'PENDING'}
(out/'results.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':summary['status'],'passed':summary['passed'],'total':summary['total'],'evidence':str(out/'results.json')},ensure_ascii=False),flush=True)
if summary['passed']!=summary['total']:raise SystemExit(1)
