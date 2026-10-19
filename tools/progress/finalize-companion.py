"""Reconcile this candidate's installed bytes, Skill mirrors and evidence on D."""
import datetime as dt
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile

ROOT=Path(__file__).resolve().parents[2]
RUN=ROOT/'.runtime/progress-panel/intake-070'
SKILL=ROOT/'Skills/arcgis-pro-2-workflows'
BUNDLE=ROOT/'配套交付/ArcGIS-Pro-MCP-2.0-Skill与面板-0.7.0候选'
PACKAGE=ROOT/'.runtime/progress-panel/activation/20261019-111256-3e136c2e/ArcGISProMCP.Compatibility.esriAddInX'
INSTALLED=Path(r'C:\Users\Administrator\Documents\ArcGIS\AddIns\ArcGISPro\{BAA5628C-3C08-4AD5-A6C0-915ADA475709}\ArcGISProMCP.Compatibility.esriAddInX')
LIVE=Path(r'D:\shiyanwenjaijia\自测记录\业务流程面板\20261019-070-final')

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def entry(p):return {'path':str(p),'bytes':p.stat().st_size,'sha256':digest(p)}
def manifest(paths):return [entry(p) for p in sorted(set(paths)) if p.is_file()]

old=read(RUN/'previous-delivery-manifest.json')
sources={Path(e['path']) for e in old['sourceManifest']}
for tree in (SKILL, ROOT/'tools/progress'):
    sources.update(p for p in tree.rglob('*') if p.is_file() and '__pycache__' not in p.parts and 'bin' not in p.parts and 'obj' not in p.parts)
sources.update([ROOT/'Skills/validation/test_skill_helpers.py',ROOT/'Skills/validation/validate_package.py',ROOT/'Skills/validation/behavior-cases.json',
 ROOT/'Docs/PROGRESS_PANEL.md',ROOT/'Skills/2.0配套Skill设计与使用说明.md',ROOT/'Docs/_gatekeeper/outbox/R-USER-20261019-intake-panel.md',ROOT/'.runtime/progress-panel/DELIVERY.md'])
assert all(p.exists() for p in sources)
mirrors=[ROOT/'.agents/skills/arcgis-pro-2-workflows',Path(r'D:\shiyanwenjaijia\配套Skill\arcgis-pro-2-workflows'),BUNDLE/'02_Skill']
files=[p for p in SKILL.rglob('*') if p.is_file()]
assert len(files)==19
for mirror in mirrors:
    for f in files:assert digest(f)==digest(mirror/f.relative_to(SKILL)),(f,mirror)
validation=read(RUN/'skill-validation.json')
assert validation['candidateDisposition']=='READY_FOR_INDEPENDENT_REVIEW'
assert all(r['result']=='PASS' for r in validation['checks'])
installer=read(RUN/'installer-checks.json');assert len(installer)==7 and all(r['result']=='PASS' for r in installer)
assert digest(PACKAGE)==digest(INSTALLED)
with zipfile.ZipFile(PACKAGE) as z:
    assert len(z.namelist())==20
    assemblies={Path(n).name:hashlib.sha256(z.read(n)).hexdigest().upper() for n in z.namelist() if n.endswith('.dll')}
loaded=read(RUN/'loaded-modules.json')
assert len(loaded)>=7 and all(e['sha256']==assemblies[e['name']] for e in loaded)
live=read(LIVE/'actual-result.json');assert live['calls']==12 and live['allInputMembersUnchanged']
assert all(digest(Path(p))==h for p,h in live['sourceHashes'].items())
catalog=read(LIVE/'live-tools-list.json')['result']['tools']
snapshot=read(SKILL/'references/tool-catalog.snapshot.json')
assert len(catalog)==239 and {t['name'] for t in catalog}=={t['name'] for t in snapshot['tools']}
allowlist=len(read(ROOT/'Config/gp-whitelist.json'))
errors=len(re.findall(r'public const string\s+\w+\s*=',(ROOT/'Source/Shared/ArcGISProMCP.Core/Results/ErrorCodes.cs').read_text(encoding='utf-8-sig')))
assert allowlist==53 and errors==33
end=read(RUN/'active-probe-701d6bd9/probe-final.json')['observations'][-1]['observedAt']
frame=read(RUN/'native-running-live300.png.observation.json')['observedAt']
parse=lambda x:dt.datetime.fromisoformat(x.replace('Z','+00:00'))
assert parse(frame)<parse(end)
original=ROOT/'Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip'
assert digest(original)=='099A4F3A575A65A1A8F9A7F424414465E676E605B12D8C1B9A7BA36E6F36B3CE'
assert digest(original)==digest(BUNDLE/'01_原版r29'/original.name)
bundle=read(BUNDLE/'companion-manifest.json')
for item in bundle['files']:assert digest(BUNDLE/item['path'])==item['sha256']
docs=read(RUN/'doc-render-verification.json');assert [r['pages'] for r in docs]==[6,6]
for r in docs:assert len(r['pageImages'])==6 and all(Path(p).exists() for p in r['pageImages'])
review={'role':'PRODUCER_VISUAL_REVIEW_NOT_INDEPENDENT_ACCEPTANCE','at':dt.datetime.now().astimezone().isoformat(),
        'pagesInspected':12,'documents':[{**entry(Path(d['docx'])),'pages':6} for d in docs],
        'renderer':'Bundled render_docx rasterizer; task-local native Word PDF adapter after recorded missing LibreOffice failure',
        'lastIteration':'Only tutorial page5 and principles page6 changed; both reinspected; other10 page PNG SHA unchanged'}
(RUN/'doc-visual-review.json').write_text(json.dumps(review,ensure_ascii=False,indent=2),encoding='utf-8')
evidence=[p for p in RUN.rglob('*') if p.is_file() and 'tampered-bundle' not in p.parts and p.name not in {'results.json','final-manifest-verification.json'}]
evidence.extend(p for p in LIVE.rglob('*') if p.is_file())
evidence.append(Path(r'D:\shiyanwenjaijia\自测记录\Skill自测与整改报告.md'))
result={'kind':'INTAKE_AND_CAPABILITY_PANEL_0_7_0_PASS_CANDIDATE','observedAt':dt.datetime.now().astimezone().isoformat(),
 'acceptance':'PENDING_INDEPENDENT_REVIEW_THIS_REVISION','previousCandidateManifest':str(RUN/'previous-delivery-manifest.json'),
 'skillVersion':'0.7.0','skillSchema':'1.3','skillCopies':3,'packagedSkillCopies':1,'filesPerCopy':19,
 'tests':{'skillHelpers':97,'skillChecks':8,'progress':16,'installer':7,'wordPagesVisuallyInspected':12},
 'runtimeActivation':{**entry(PACKAGE),'installedSha256':digest(INSTALLED),'processId':11392,'loadedModules':len(loaded),
   'loadedModuleChecks':loaded,'registeredTools':239,'errorCodes':errors,'gpAllowlist':allowlist,'release':False,'proSupport':'3.5/net8 local test'},
 'liveWorkflow':{'finalCalls':12,'earlierSameRevisionCalls':12,'finalEvidence':str(LIVE),'sourceFilesUnchanged':15,
   'uiStressRuns':[90,90,300],'stressIsNotAdditionalCapabilityCoverage':True,'qualifiedRunningScreenshot':str(RUN/'native-running-live300.png'),
   'screenshotAt':frame,'executionEndAt':end,'earlierUnqualifiedFramesRetained':True},
 'companionDirectory':str(BUNDLE),'originalR29':entry(original),'wordDocs':review['documents'],
 'limits':['Independent acceptance pending; no executor self PASS','New plugin candidate only live-tested on Pro3.5/net8',
   'MCP-only remote clients have no dedicated plan publication tool','Hydrology Fill/D8/Watershed execution not implemented by this work',
   '97 helper tests and82 designed behavior cases are not97 or82 real model evaluations','Panel is read-only; chat updates require Agent messages'],
 'sourceManifest':manifest(sources),'buildArtifactManifest':manifest([Path(e['path']) for e in old['buildArtifactManifest']]+[PACKAGE,INSTALLED]+[p for p in BUNDLE.rglob('*') if p.is_file()]),
 'evidenceManifest':manifest(evidence),'hashBoundary':'Excludes mutable delivery manifest and final verification/result receipts; identities are not scientific certification.'}
target=ROOT/'.runtime/progress-panel/delivery-manifest.json'
target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
summary={k:v for k,v in result.items() if k not in {'sourceManifest','buildArtifactManifest','evidenceManifest'}}
(RUN/'results.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
checks={k:{'files':len(result[k]),'differences':[e['path'] for e in result[k] if digest(Path(e['path']))!=e['sha256']]}
        for k in ('sourceManifest','buildArtifactManifest','evidenceManifest')}
assert all(not r['differences'] for r in checks.values())
(RUN/'final-manifest-verification.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':'PASS CANDIDATE','manifests':checks,'companion':str(BUNDLE)},ensure_ascii=False))
