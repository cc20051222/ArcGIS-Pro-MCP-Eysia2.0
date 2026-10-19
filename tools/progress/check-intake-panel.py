"""Bounded live read-only UI stress probe; never edits GIS data."""
import copy
import datetime as dt
import json
from pathlib import Path
import sys
import urllib.request
import uuid
import argparse

args=argparse.ArgumentParser();args.add_argument('--calls',type=int,default=90);count=args.parse_args().calls
if not 1<=count<=500:raise ValueError('Bounded read-only probe requires 1..500 calls')

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Skills/arcgis-pro-2-workflows/scripts'))
import publish_workflow as publisher

run=ROOT/('.runtime/progress-panel/intake-070/active-probe-'+uuid.uuid4().hex[:8])
run.mkdir(exist_ok=False)
target=ROOT/'.runtime/progress-panel/task-record.json'
old=json.loads(target.read_text(encoding='utf-8'))
r=copy.deepcopy(old)
r.pop('workflowView',None)
r['execution']['status']='RUNNING'
r.update(taskId='highlight-'+uuid.uuid4().hex,goal='当前步骤高亮与快速只读事件同步检查',recordOrigin='ACTUAL_READ_ONLY_UI_STRESS_PROBE')
r['quality']=json.loads((ROOT/'Skills/arcgis-pro-2-workflows/assets/task-record.json').read_text(encoding='utf-8'))['quality']
r['observations']=[];r['presentation']['artifacts']=[];r['presentation']['lastObservedAt']=None
r['intake']['outputRoot']['value']=str(run)
r['intake']['toolChoices']=[{'stepId':'P02','status':'SUPPORTED','tools':['validate_geometries'], 'reason':'本轮已实测239工具及几何检查，受控只读压力观察', 'evidenceRefs':['U']}]
for field in ('requirements','inputs','outputRoot'):r['intake'][field]['evidenceRefs']=['U']
r['intake']['requirements']['value']='用户要求检查当前步骤高亮与双显示，限定既有数据只读测试'
r['plan'].update(revision=1,changes=[],steps=[
 {'id':'P01','title':'已明确的数据与输出位置','state':'VERIFIED','kind':'PREFLIGHT','dependsOn':[],'evidenceRefs':['U'],'requestIds':[]},
 {'id':'P02','title':'实时高亮与只读事件连续性检查','state':'PLANNED','kind':'PROCESS','dependsOn':['P01'],'evidenceRefs':[],'requestIds':[]},
 {'id':'P03','title':'收尾检查与恢复业务任务视图','state':'PLANNED','kind':'FINAL','dependsOn':['P02'],'evidenceRefs':[],'requestIds':[]}])
def obs(key,summary,actor=None):
 e={'id':key,'source':'ACTUAL_READ_ONLY_UI_PROBE','summary':summary,'observedAt':dt.datetime.now().astimezone().isoformat()}
 if actor:e['actor']=actor
 r['observations'].append(e);r['presentation']['lastObservedAt']=e['observedAt']
obs('U','沿用用户的需求和D盘数据/输出授权；当前操作只读，测试快速事件与手动选行并存','USER')
publisher.publish(r,target,old['taskId'],True)
try:
 for i in range(count):
  rid=r['taskId']+'-'+str(i);s=r['plan']['steps'][1];s['requestIds'].append(rid)
  obs('S'+str(i),'派发第 '+str(i+1)+' / '+str(count)+' 次有界只读界面压力请求；不是新增业务覆盖')
  s.update(state='RUNNING',evidenceRefs=['S'+str(i)],summary='正在按界限检查快速事件连续显示；允许手动查看P01')
  publisher.publish(r,target)
  q={'jsonrpc':'2.0','id':rid,'method':'tools/call','params':{'name':'validate_geometries','arguments':{'dataset':r['intake']['inputs']['value'][i%3],'minimumSegmentLength':.001,'maxFeatures':1000000}}}
  req=urllib.request.Request('http://127.0.0.1:6520/mcp',data=json.dumps(q).encode(),headers={'Content-Type':'application/json'})
  with urllib.request.urlopen(req,timeout=30) as response:value=json.load(response)
  (run/(str(i)+'.json')).write_text(json.dumps(value,ensure_ascii=False),encoding='utf-8')
  if value.get('error') or value.get('result',{}).get('isError'):raise ValueError(value)
 obs('END',str(count)+'次只读调用已返回；不扩大资料/拓扑规则覆盖')
 r['plan']['steps'][1].update(state='VERIFIED',evidenceRefs=['END'])
 r['plan']['steps'][2].update(state='VERIFIED',evidenceRefs=['END'])
 publisher.publish(r,target)
 (run/'probe-final.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
finally:
 # Explicitly restore the separately validated user's task; probe is not a substitute for quality acceptance.
 publisher.publish(old,target,r['taskId'],True)
print(json.dumps({'calls':count,'recordRestored':old['taskId'],'run':str(run)},ensure_ascii=False))
