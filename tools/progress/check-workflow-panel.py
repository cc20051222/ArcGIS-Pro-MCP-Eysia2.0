"""Prepare explicit hydrology UI fixtures or publish actual read-only noise-zone QA.
Never runs hydrology, repair, arbitrary GIS code or a new service.
"""
import argparse,copy,datetime as dt,hashlib,json,pathlib,sys,time,urllib.request,uuid
ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Skills/arcgis-pro-2-workflows/scripts'))
import publish_workflow as publisher

def save(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
def fresh(goal,origin):
    r=json.loads((ROOT/'Skills/arcgis-pro-2-workflows/assets/task-record.json').read_text(encoding='utf-8'))
    r.update(taskId='workflow-'+uuid.uuid4().hex,goal=goal,recordOrigin=origin)
    r['quality']['producerId']='current-agent';r['quality']['subjectVersions']={'data':'v1'}
    return r
def observation(record,summary,source='ACTUAL_MCP',at=None):
    key='E'+str(len(record['observations'])+1)
    record['observations'].append({'id':key,'source':source,'observedAt':at or dt.datetime.now().astimezone().isoformat(),'summary':summary})
    record['presentation']['lastObservedAt']=record['observations'][-1]['observedAt'];return key
def step(key,title,depends=(),kind='PROCESS',**extras):
    return {'id':key,'title':title,'state':'PLANNED','dependsOn':list(depends),'evidenceRefs':[],
            'kind':kind,'requestIds':[],'requirementIds':[],**extras}
def req(key,title,phases):
    return {'id':key,'title':title,'criterion':'按声明范围重新读取；无必需缺陷且覆盖完整',
            'source':'本次明确的通用技术检查；非行业认证','required':True,'phases':phases,'subjectIds':['data'],'coverageRequired':'FULL'}
def check(r,key,phase,state,refs,result,population,check_id=None):
    return {'id':check_id or key+'-'+phase,'requirementId':key,'phase':phase,'state':state,
            'method':'正常MCP只读重读及文件SHA256对账','profileRevision':1,'bindings':{'data':r['quality']['subjectVersions']['data']},
            'scope':{'mode':'FULL','population':population,'checked':population},'evidenceRefs':refs,'result':result}

def demo(run):
    r=fresh('DEM水文分析完整流程（界面演示，未执行GIS）','SYNTHETIC_NOT_LIVE_HYDROLOGY_UI_FIXTURE')
    r.pop('intake',None)  # Preserve the earlier UI fixture as explicitly unconfirmed, never a production plan.
    r['schemaVersion']='1.2'
    r['plan'].update(scenario='水文分析 · 按目标选择方法',selectionReason='方法范例：指定出口汇水区→D8链；没有真实DEM，不发出GIS请求。',
        constraints=['当前2.0受控接口未覆盖Fill/Flow Direction/Flow Accumulation/Watershed；真实任务须标受阻。','软件方法依据不等于行业标准认证；真实洼地、河网阈值和出口点须另定。'])
    rows=[
        ('资料、需求与规范预检','CRS/高程基准、有效范围、许可与上游边界','PREFLIGHT'),
        ('DEM预处理','拼接/投影/格网对齐；NoData与重采样','PROCESS'),
        ('洼地诊断与填洼 Fill','保留真实洼地；填洼深度与改动量','PROCESS'),
        ('流向计算','D8/MFD/DINF与下游算法兼容','PROCESS'),
        ('汇流累计量计算','像元数/面积/权重与边界影响','PROCESS'),
        ('河网阈值与敏感性','参照河网、面积口径和多阈值比较','PROCESS'),
        ('提取河网与分级','连通性、背景NoData、分段与分级','PROCESS'),
        ('河网矢量化','下游方向、断裂和合理河源悬挂','PROCESS'),
        ('出口点定位与吸附','出口ID、位移、目标支流及有据距离','PROCESS'),
        ('流域与子流域划分','出口对应、嵌套或互斥分区、范围完整性','PROCESS'),
        ('水文地形指标统计','面积/河长/坡度/密度的单位与分母','PROCESS'),
        ('成果全面首检','格网/数值/网络/流域/统计/制图分别检查','CHECK'),
        ('问题修复与下游重算','变更依据、原件保护、下游标失效','REPAIR'),
        ('新证据全量复检','重新测量、原问题与受影响链逐项闭环','RECHECK'),
        ('最终交付检查','完整成员、版本、实际可读与真实视觉','FINAL'),
        ('外部验收','独立裁定，制作端不自批','ACCEPTANCE')]
    for i,(title,focus,kind) in enumerate(rows):
        r['plan']['steps'].append(step('H'+str(i+1).zfill(2),title,[] if i==0 else ['H'+str(i).zfill(2)],kind,
            method=focus,checkFocus=[focus],required=kind!='ACCEPTANCE',summary='尚未执行；真实步骤与参数由Agent按资料制定'))
    r['quality']['requirements']=[req('R-HYDRO','河网阈值适用性（演示）',['PROCESS','RECHECK'])]
    r['quality']['standardMappings']=[{'source':'Esri Pro3.5 Hydrology/Watershed方法文档','scope':'D8/出口点/受控方法依据；非行业标准证书'}]
    r['plan']['steps'][5]['requirementIds']=['R-HYDRO']
    r['plan']['steps'][12]['requirementIds']=['R-HYDRO']
    save(run/'hydrology-plan.json',r)
    # A separate, prominently labelled synthetic repair state; never represented as real hydrology.
    repair=copy.deepcopy(r);repair['plan']['revision']=2;repair['quality']['boundPlanRevision']=2
    repair['plan']['changes']=[{'reason':'界面测试：阈值检查失败，演示修复分支；不是实际分析','affected':['H06','H07','H08','H09','H10','H11']}]
    for s in repair['plan']['steps'][:5]:
        e=observation(repair,'演示已核验 '+s['title'],'SYNTHETIC_NOT_LIVE');s.update(state='VERIFIED',evidenceRefs=[e])
    failed=observation(repair,'演示反例：阈值导致河网过密，需重新选择；没有真实数据','SYNTHETIC_NOT_LIVE')
    repair['plan']['steps'][5].update(state='FAILED',evidenceRefs=[failed])
    start=observation(repair,'演示：正在处置阈值问题，未运行GIS','SYNTHETIC_NOT_LIVE')
    repair['plan']['steps'][12].update(state='RUNNING',dependsOn=['H06'],evidenceRefs=[start],summary='界面演示：修复中，下游暂不执行')
    repair['plan']['steps'][6]['dependsOn']=['H13']
    repair['plan']['steps'][13]['dependsOn']=['H12','H13']
    repair['quality']['checks']=[check(repair,'R-HYDRO','PROCESS','FAIL',[failed],'演示：河网过密，不满足该示例判据',1)]
    repair['quality']['issues']=[{'id':'DEMO-I1','severity':'MAJOR','status':'OPEN','description':'演示缺陷：河网阈值缺乏适用性，不能直接采用。',
        'repair':{'state':'REPAIRING','action':'演示：对比候选阈值、重新选择；随后重算受影响下游','affectedStepIds':['H06','H07','H08','H10','H11'],'recheckResult':'等待新证据；尚未复检'}}]
    save(run/'hydrology-repair-demo.json',repair)
    recheck=copy.deepcopy(repair);recheck['plan']['revision']=3;recheck['quality']['boundPlanRevision']=3
    recheck['plan']['changes'].append({'reason':'界面测试：演示修复完成后下游重算与新证据复检；无真实水文运行','affected':['H06','H07','H08','H10','H11','H14']})
    old=recheck['quality']['checks'][0];recheck['quality']['history'].append({'check':copy.deepcopy(old)})
    recheck['quality']['subjectVersions']['data']='demo-fixed-v2'
    fixed=observation(recheck,'演示：方法修正及下游重算完成，首检重新读回通过','SYNTHETIC_NOT_LIVE')
    for s in recheck['plan']['steps'][5:13]:s.update(state='VERIFIED',evidenceRefs=[fixed],summary='演示修复后新结果已检查；未运行真实GIS')
    recheck['quality']['checks']=[check(recheck,'R-HYDRO','PROCESS','PASS',[fixed],'演示：修正后首检通过，旧失败保留在历史',1)]
    dispatched=observation(recheck,'演示：已开始新证据复检','SYNTHETIC_NOT_LIVE')
    recheck['plan']['steps'][13].update(state='RUNNING',evidenceRefs=[dispatched],summary='界面演示：复检中，未形成最终通过')
    recheck['quality']['issues'][0]['repair'].update(state='RECHECKING',recheckResult='等待本次新复检结果')
    save(run/'hydrology-rechecking-demo.json',recheck)
    closed=copy.deepcopy(recheck);reviewed=observation(closed,'演示：采用新的读回证据，复检通过','SYNTHETIC_NOT_LIVE')
    closed['plan']['steps'][13].update(state='VERIFIED',evidenceRefs=[reviewed],summary='演示复检通过；最终交付与外部验收仍未执行')
    c=check(closed,'R-HYDRO','RECHECK','PASS',[reviewed],'演示：新版本复检通过，非复制首检',1);c['recheckOf']='R-HYDRO-PROCESS'
    closed['quality']['checks'].append(c)
    closed['quality']['issues'][0].update(status='RESOLVED',resolutionEvidenceRefs=[reviewed])
    closed['quality']['issues'][0]['repair'].update(state='RESOLVED',recheckResult='演示：已修复并复检；没有真实水文执行')
    save(run/'hydrology-closed-demo.json',closed)
    publisher.publish(r,run/'demo-active.json')
    print(json.dumps({'demo':str(run/'demo-active.json'),'plan':str(run/'hydrology-plan.json'),'repair':str(run/'hydrology-repair-demo.json')},ensure_ascii=False))

def live(run,target):
    r=fresh('强、中、弱噪声分区：全量质检与复检','ACTUAL_READ_ONLY_QA_NOT_HYDROLOGY')
    r['mode']='inspect';r['plan'].update(scenario='既有GIS成果质量检查',selectionReason='读取实际三份成果；按面几何和环形分区适用规则检查，不重跑数据制作。',constraints=['实际执行只读；无缺陷则如实跳过修复。','非路灯/日照/三维或完整考试验收。'])
    r['execution']['status']='RUNNING'
    tasks=[('A01','输入与阈值预检','PREFLIGHT'),('A02','强影响区几何质检','PROCESS'),('A03','中影响区几何质检','PROCESS'),('A04','弱影响区几何质检','PROCESS'),('A05','三区适用拓扑首检','CHECK'),('A06','发现问题后的处置','REPAIR'),('A07','重新读取并全量复检','RECHECK'),('A08','最终成员与输入完整性检查','FINAL'),('A09','外部验收','ACCEPTANCE')]
    for i,(key,title,kind) in enumerate(tasks):
        dep=[] if i==0 else [tasks[i-1][0]]
        if key=='A07':dep=['A05']
        r['plan']['steps'].append(step(key,title,dep,kind,required=key not in {'A06','A09'},checkFocus=['全量检查、实际结果与证据','原件保护及当前版本']))
    r['quality']['requirements']=[req('SRC','输入资料与容差',['PREFLIGHT']),req('GEO','三区四项几何质量',['PROCESS','RECHECK']),req('TOP','三区适用拓扑',['PROCESS','RECHECK']),req('END','最终完整性',['FINAL'])]
    for s in r['plan']['steps']:
        s['requirementIds']=['SRC'] if s['id']=='A01' else ['GEO'] if s['id'] in {'A02','A03','A04'} else ['TOP'] if s['id']=='A05' else ['GEO','TOP'] if s['id']=='A07' else ['END'] if s['id']=='A08' else []
    data=[pathlib.Path(r'D:\shiyanwenjaijia\数据结果')/(n+'.shp') for n in ['NoiseStrong','NoiseMedium','NoiseWeak']]
    members=[p.with_suffix(s) for p in data for s in ['.shp','.shx','.dbf','.prj','.cpg']]
    initial={str(p):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in members}
    u=observation(r,'沿用用户已给出的 D:\\shiyanwenjaijia 数据/成果根；本次按用户要求只读检查面板与既有成果。','CURRENT_CONVERSATION_ALREADY_PROVIDED')
    r['observations'][-1]['actor']='USER'
    discovery={'jsonrpc':'2.0','id':r['taskId']+'-catalog','method':'tools/list','params':{}}
    request=urllib.request.Request('http://127.0.0.1:6520/mcp',data=json.dumps(discovery).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(request,timeout=30) as response:catalog=json.load(response)
    save(run/'live-tools-list.json',catalog)
    tools={t['name'] for t in catalog['result']['tools']}
    if not {'validate_geometries','check_topology_rules'}.issubset(tools):raise ValueError('Current quality tools unavailable')
    t=observation(r,'当前 tools/list 已取得两项工具及schema；本机已有实现证据，本轮再实际只读复检。','ACTUAL_TOOLS_LIST')
    r['intake']={'requirements':{'status':'CONFIRMED','value':'新版业务面板与既有三区成果只读质检回归','evidenceRefs':[u]},
                 'inputs':{'status':'CONFIRMED','value':[str(p) for p in data],'evidenceRefs':[u]},
                 'outputRoot':{'status':'CONFIRMED','value':str(run),'evidenceRefs':[u]},'dataNeeds':[],
                 'toolChoices':[{'stepId':s['id'],'status':'SUPPORTED','tools':['validate_geometries'],
                                 'reason':'当前schema及已有实际实现观测；本轮仍检查覆盖与响应','evidenceRefs':[t]}
                                for s in r['plan']['steps'] if s['id'] in {'A02','A03','A04'}] + [
                                {'stepId':key,'status':'SUPPORTED','tools':names,'reason':'当前schema和本机实际只读实现','evidenceRefs':[t]}
                                for key,names in [('A05',['check_topology_rules']),('A07',['validate_geometries','check_topology_rules'])]]}
    r['quality']['subjectVersions']['data']=hashlib.sha256(json.dumps(initial,sort_keys=True).encode()).hexdigest()
    prior=json.loads(target.read_text(encoding='utf-8-sig')) if target.exists() else {}
    publisher.publish(r,target,prior.get('taskId','LEGACY'),True)
    def emit():save(run/'actual-task.json',r);publisher.publish(r,target)
    def mark(s,state,summary,source='ACTUAL_FILE_READ'):
        e=observation(r,summary,source);s.update(state=state,summary=summary,evidenceRefs=[e]);emit();return e
    def call(s,name,args):
        rid=r['taskId']+'-'+str(len(r['observations'])+1);s['requestIds'].append(rid)
        mark(s,'RUNNING','已派发实际只读 '+name,'ACTUAL_DISPATCH')
        q={'jsonrpc':'2.0','id':rid,'method':'tools/call','params':{'name':name,'arguments':args,'_meta':{'progressToken':rid}}}
        # JSON transport remains valid with a token; local native events still correlate by request ID.
        req_=urllib.request.Request('http://127.0.0.1:6520/mcp',data=json.dumps(q).encode(),headers={'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(req_,timeout=120) as resp:response=json.load(resp)
            save(run/(rid+'.json'),{'request':q,'response':response})
            if response.get('error') or response.get('result',{}).get('isError'):raise ValueError(response)
            value=json.loads(response['result']['content'][0]['text'])
            if value.get('qualityVerdict')!='PASS' or not value.get('coverageComplete') or value.get('scannedCount')!=value.get('totalFeatureCount'):raise ValueError(value)
            e=mark(s,'PRODUCED',name+' 已返回完整结果，待业务核对','ACTUAL_MCP')
            return value,e
        except Exception as ex:
            mark(s,'FAILED',str(ex),'ACTUAL_ERROR');raise
    # Inputs are known existing outputs; .001 is independently confirmed as each XYTolerance below.
    e=mark(r['plan']['steps'][0],'VERIFIED','三份既有SHP及15个核心成员可读；.001m为数值健康阈值，调用后核各自XYTolerance')
    r['quality']['checks'].append(check(r,'SRC','PREFLIGHT','PASS',[e],'15成员已留初始SHA',15));emit()
    geo=[];top=[]
    for s,path in zip(r['plan']['steps'][1:4],data):
        s.update(inputs=[str(path)],method='四项几何全量；短边0.001m=数据集XYTolerance，不是行业精度')
        value,e=call(s,'validate_geometries',{'dataset':str(path),'minimumSegmentLength':.001,'maxFeatures':1000000})
        if value['xyTolerance']!=.001 or not value['allRequestedChecksApplied']:raise ValueError('Threshold/applicability changed')
        geo.append(e);mark(s,'VERIFIED','实际全量1/1、四项已应用、0缺陷；XYTolerance=.001m','ACTUAL_RESULT_CHECK')
    r['quality']['checks'].append(check(r,'GEO','PROCESS','PASS',geo,'三区均全量1/1；四项检查0缺陷',3));emit()
    s=r['plan']['steps'][4];s['method']='无自交与单外壳；环形内孔符合业务，不误加禁止孔洞'
    for path in data:
        value,e=call(s,'check_topology_rules',{'dataset':str(path),'rules':['must_not_self_intersect','must_be_single_part']});top.append(e)
    mark(s,'VERIFIED','三区适用拓扑全量通过，原响应已保留','ACTUAL_RESULT_CHECK')
    r['quality']['checks'].append(check(r,'TOP','PROCESS','PASS',top,'三区全量1/1，适用两规则0违规',3));emit()
    mark(r['plan']['steps'][5],'SKIPPED','本次未检出缺陷，无需修复；不编造修复动作')
    s=r['plan']['steps'][6];again={'GEO':[],'TOP':[]}
    for path in data:
        for key,name,args in [('GEO','validate_geometries',{'dataset':str(path),'minimumSegmentLength':.001,'maxFeatures':1000000}),('TOP','check_topology_rules',{'dataset':str(path),'rules':['must_not_self_intersect','must_be_single_part']})]:
            _,e=call(s,name,args);again[key].append(e)
    mark(s,'VERIFIED','六次新请求重新读取三区，全部全量通过；不是复制首检证据','ACTUAL_RESULT_CHECK')
    for key in ['GEO','TOP']:
        c=check(r,key,'RECHECK','PASS',again[key],'新读回全部通过',3);c['recheckOf']=key+'-PROCESS';r['quality']['checks'].append(c)
    emit()
    current={str(p):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in members}
    if current!=initial:raise ValueError('Input member changed during read-only QA')
    e=mark(r['plan']['steps'][7],'VERIFIED','15个成员SHA前后不变；全部实际请求与检查已归档；不等同完整考试')
    r['quality']['checks'].append(check(r,'END','FINAL','PASS',[e],'15成员完整且未修改',15));r['quality']['acceptance']['status']='WAITING_REVIEW';emit()
    save(run/'actual-result.json',{'status':'PASS CANDIDATE','calls':12,'allInputMembersUnchanged':True,'sourceHashes':current,'panelRecord':str(target),'taskId':r['taskId']})
    delivery=observation(r,'实际报告和原始任务记录可读，逐件公布位置；不把计划路径当成果','ACTUAL_DELIVERY_FILE_CHECK')
    r['presentation']['artifacts']=[{'label':label,'stepId':'A08','state':'VERIFIED','path':str(run/name),'observationRef':delivery,'isPreview':False}
                                   for label,name in [('检查结果报告（JSON）','actual-result.json'),('任务与检查原始记录（JSON）','actual-task.json')]]
    r['execution']['status']='COMPLETED'
    emit()
    print(json.dumps({'status':'PASS CANDIDATE','calls':12,'run':str(run),'taskId':r['taskId']},ensure_ascii=False))

def main():
    p=argparse.ArgumentParser();p.add_argument('mode',choices=['demo','live']);p.add_argument('--run',required=True,type=pathlib.Path);p.add_argument('--target',type=pathlib.Path)
    a=p.parse_args();publisher.plain_d(a.run);a.run.mkdir(parents=True,exist_ok=False)
    if a.mode=='demo':demo(a.run)
    elif a.target is None:raise ValueError('A known configured --target is required')
    else:live(a.run,a.target)
if __name__=='__main__':main()
