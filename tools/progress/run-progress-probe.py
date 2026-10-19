"""Single real buffer call for the running-panel observation. No write retries."""
import datetime as dt,json,pathlib,sys,urllib.request
run=pathlib.Path(sys.argv[1]).resolve()
if run.drive.upper()!='D:':raise ValueError('D root required')
fixture=json.loads((run/'fixtures.json').read_text(encoding='utf-8-sig'))
output=run/'progress-buffer.shp'
if output.exists() or (run/'progress-request.json').exists():raise ValueError('Existing request/output: reconcile instead of retry')
request={'jsonrpc':'2.0','id':'quality-fix-live-buffer','method':'tools/call','params':{'name':'buffer','arguments':{
    'input':fixture['paths']['gp_stress_input'],'output':str(output),'distance':3,'distanceUnit':'Meters','dissolve':False,'overwrite':False},
    '_meta':{'progressToken':'quality-fix-live-token'}}}
(run/'progress-request.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
records=[]; final=None; error=None
try:
    r=urllib.request.Request('http://127.0.0.1:6520/mcp',data=json.dumps(request).encode('utf-8'),headers={'Content-Type':'application/json','Accept':'application/json, text/event-stream'})
    with urllib.request.urlopen(r,timeout=180) as response,(run/'progress-sse.jsonl').open('x',encoding='utf-8') as log:
        if 'text/event-stream' not in response.headers.get('Content-Type',''):raise ValueError('Expected real SSE progress stream')
        lines=[]
        for raw in response:
            line=raw.decode('utf-8').rstrip('\r\n')
            if line.startswith('data:'):lines.append(line[5:].lstrip())
            elif not line and lines:
                message=json.loads('\n'.join(lines));lines=[]
                entry={'at':dt.datetime.now().astimezone().isoformat(),'message':message};records.append(entry)
                log.write(json.dumps(entry,ensure_ascii=False)+'\n');log.flush()
                if message.get('id')==request['id']:final=entry
except Exception as ex:error=type(ex).__name__+': '+str(ex)
events=[r for r in records if r['message'].get('method')=='notifications/progress']
sdk=[r for r in events if r['message']['params'].get('_meta',{}).get('execution',{}).get('Source','').startswith('ArcGIS.GP.')]
valid_final=bool(final and not error and not final['message'].get('error') and isinstance(final['message'].get('result'),dict) and not final['message']['result'].get('isError'))
summary={'endedAt':dt.datetime.now().astimezone().isoformat(),'requestId':request['id'],'output':str(output),'events':len(events),'sdkEvents':len(sdk),'firstSdkAt':sdk[0]['at'] if sdk else None,'final':final,'error':error,'status':'PRODUCED_PENDING_RECHECK' if valid_final else 'FAILED_OR_UNKNOWN'}
(run/'progress-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False),flush=True)
if error or summary['status']=='FAILED_OR_UNKNOWN':raise SystemExit(1)
