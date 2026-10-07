from pathlib import Path
from urllib.parse import unquote
from collections import Counter
import hashlib, re, json, zipfile

ROOT=Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案')
CHECK=ROOT/'99_来源与核验'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def read(path): return path.read_text(encoding='utf-8-sig')
provenance=json.loads(read(CHECK/'技术详稿来源与链接迁移.json'))
revisions={x['finalPath']:x for x in provenance.get('currentRevisions',[])}
source_integrity=[]
for rec in provenance['files']:
    original=Path(rec['originalPath']); copy=ROOT/rec['finalPath']
    assert sha(original)==rec['originalSha256'], str(original)
    current=revisions.get(rec['finalPath'])
    assert sha(copy)==(current['currentSha256'] if current else rec['finalSha256']), str(copy)
    if original.suffix=='.md':
        pattern=r'(\]\()([^\)]+)(\))'
        normalized=lambda text: re.sub(pattern,r'\1LINK_DESTINATION\3',text).replace('\r\n','\n')
        if not current:
            assert normalized(read(original))==normalized(read(copy)), str(copy)
        else:
            # Revised copies retain original technical paragraphs. The leading
            # revision/date metadata is explicitly allowed to change.
            body=normalized(read(copy))
            metadata=lambda text: re.sub(r'(?:完善)?日期：20\d{2}-\d{2}-\d{2}(?:（资产物化合同深化）)?', '日期：DATE',text)
            for index,paragraph in enumerate(normalized(read(original)).split('\n\n')):
                if index<3 and (paragraph.startswith(('> 修订','> 版本','日期：'))):
                    assert metadata(paragraph.strip()) in metadata(body),str(copy)
                    continue
                if paragraph.strip(): assert paragraph.strip() in body, str(copy)
    else: assert original.read_bytes()==copy.read_bytes()
    source_integrity.append({'original':str(original),'originalUnchanged':True,
        'copy':str(copy.relative_to(ROOT)), 'copyStatus':'AUTHORIZED_IN_PLACE_REVISION' if current else 'INITIAL_LINK_MIGRATION_UNCHANGED'})

allmd=sorted(p for p in ROOT.rglob('*.md') if p.relative_to(ROOT).parts[0]!='宣传')
missing=[]; checked=0
link_re=re.compile(r'\]\((<[^>]+>|[^)]+)\)')
for p in allmd:
    body=read(p)
    assert '\ufffd' not in body, f'Replacement character in {p}'
    for value in link_re.findall(body):
        value=value.strip('<>')
        if value.startswith(('https://','http://','mailto:','codex://','#')): continue
        pathstr=unquote(value.split('#')[0])
        if re.match(r'^/[A-Za-z]:/',pathstr): pathstr=pathstr[1:]
        path=Path(pathstr)
        if not path.is_absolute(): path=p.parent/path
        checked+=1
        if not path.exists(): missing.append({'from':str(p.relative_to(ROOT)),'link':value,'resolved':str(path)})
assert not missing, json.dumps(missing,ensure_ascii=False)

spec=json.loads(read(ROOT/'最终方案范围与状态.json'))
baseline=spec['originalQualityBaseline']
assert (baseline['semanticBaseline'],baseline['psOperations'],baseline['templates'],baseline['standardImages'],baseline['coreScenarios'],baseline['newUsers'],baseline['minimumNewUserTasks'])==(285,20,36,108,60,24,48)
assert baseline['psManualDesign']==0 and baseline['severeScientificErrors']==0 and baseline['wrongTargetWrites']==0
assert baseline['allOriginalReleaseGatesRequired'] and baseline['noThresholdReduction']
assert len(spec['workItems'])==30 and len({r['id'] for r in spec['workItems']})==30
assert len(spec['innovations'])==12 and len(spec['feasibilityPrototypes'])==8
assert spec['productAcceptance']=='NOT_RUN' and spec['fullScope60DayFeasibility']=='UNKNOWN_UNTIL_MEASURED'
assert spec['startDate'] is None and spec['targetCalendarDays']==60
gate=ROOT/baseline['gateSource']
assert gate.exists() and '2. 发行完成门' in read(gate)
historic_gate=Path(baseline['frozenHistoricalGateSource']['path'])
assert sha(historic_gate)==baseline['frozenHistoricalGateSource']['sha256']
def gate_table(path):
    lines=read(path).replace('\r\n','\n').split('2. 发行完成门',1)[1].splitlines()
    rows=[]
    for line in lines[1:]:
        if line.startswith('|'): rows.append(line)
        elif rows: break
    assert len(rows)==20
    return '\n'.join(rows)
assert gate_table(gate)==gate_table(historic_gate)
assert hashlib.sha256(gate_table(gate).encode('utf-8')).hexdigest().upper()==baseline['frozenHistoricalGateSource']['gateTableSha256']
model=json.loads(read(ROOT/'04_工期与效率优化/资料与计算/工期计算模型.json'))
work_projection=lambda rows: {r['id']:(r['owner'],tuple(r['dependencies'])) for r in rows}
assert work_projection(spec['workItems'])==work_projection(model['workItems'])
assert len(model['originalGateIds'])==18 and len(model['additionalGateIds'])==11
assert {x['id'] for x in spec['inPlaceRefinements']}=={'R12-ACQUISITION','R12-MILESTONES','R12-CAPACITY','R13-TIME','R13-RETENTION','R14-UPDATE-TRUST','R14-CONTENT-FLOW'}
assert model['lifecycleCostLedger']['notAddedTwiceToOriginalWiTotals']
assert model['lifecycleCostLedger']['notContingencyWork']
for refinement in spec['inPlaceRefinements']:
    assert (ROOT/refinement['canonicalDocument']).exists()

docxs=sorted((ROOT/'06_Word项目介绍').glob('*.docx'))
assert len(docxs)==1, 'Expected one final introduction Word document'
with zipfile.ZipFile(docxs[0]) as z:
    assert z.testzip() is None
    xml=z.read('word/document.xml').decode('utf-8')
    assert all(term in xml for term in ['StyleDNA','DatasetDNA','Photoshop','2100','3000','FOUNDATION_GATE','C0','E1','E2','E3','E4','E5','ResumeDecision'])
    assert 'placeholder' not in xml.lower()
    import xml.etree.ElementTree as ET
    root=ET.fromstring(z.read('word/document.xml'))
    w='{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
    text=''.join((node.text or '') for node in root.iter(w+'t'))
    assert '\ufffd' not in text
    word_chars=len(text)
    table_count=sum(1 for node in root.iter(w+'tbl'))

native=CHECK/'Word排版'/'native-word-render.json'
native_record=json.loads(read(native)) if native.exists() else {'success':False,'status':'NOT_VERIFIED'}
generation=json.loads(read(CHECK/'Word排版/Word生成记录.json'))
assert generation['sha256']==sha(docxs[0])
assert len(generation['sourceBodies'])==4
for source in generation['sourceBodies']:
    assert sha(Path(source['path']))==source['sha256'], source['path']
pages=json.loads(read(CHECK/'Word排版/最终页面指纹.json'))
layout=json.loads(read(CHECK/'Word排版/最终Word排版核验.json'))
assert layout['docxSha256']==pages['docxSha256']==sha(docxs[0])
assert layout['pdfSha256']==pages['pdfSha256']==sha(CHECK/'Word排版/项目介绍原生分页.pdf')
assert layout['pages']==pages['pages']==native_record['pageCount']
assert set(layout['visualCoveragePages'])==set(range(1,pages['pages']+1))
for page in pages['images']: assert sha(Path(page['path']))==page['sha256']
review=CHECK/'修订与交叉审读闭环.json'
review_record=json.loads(read(review)) if review.exists() else {'status':'PENDING'}
report={'kind':'FINAL_DOCUMENT_PACKAGE_CHECK','date':'2026-10-03','status':'DOCUMENT_CHECKS_COMPLETED',
        'productAcceptance':'NOT_RUN','performanceMeasured':False,'completeScheduleFeasibility':'UNKNOWN',
        'markdownCount':len(allmd),'finalDocxCount':len(docxs),'technicalOriginalMarkdownCount':15,
        'localLinksChecked':checked,'missingLocalLinks':missing,'retainedSources':source_integrity,
        'qualityBaseline':baseline,'workItems':len(spec['workItems']),'owners':dict(Counter(x['owner'] for x in spec['workItems'])),
        'innovations':len(spec['innovations']),'feasibilityPrototypes':len(spec['feasibilityPrototypes']),
        'wordTextCharacters':word_chars,'wordTables':table_count,'nativeWordRender':native_record,
        'independentDocumentReview':review_record}
report['currentRefinements']=spec['inPlaceRefinements']
report['wordSourceBodiesCurrent']=True
report['frozenOriginalGateTableUnchanged']=True
report['fullWiOwnerAndDependencyProjectionUnchanged']=True
report['advertisingBoundary']='Separate existing advertising manifests; excluded from core package count and manifest'
(CHECK/'最终资料核验结果.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

records=[]
for p in sorted(ROOT.rglob('*')):
    if not p.is_file():continue
    relative=p.relative_to(ROOT).as_posix()
    if relative.startswith('宣传/'): continue
    if relative.startswith('99_来源与核验/临时/') or relative.startswith('99_来源与核验/Word排版/'):
        continue
    if p.name in ['最终资料文件清单.json','最终资料核验结果.json']:continue
    records.append({'path':relative,'bytes':p.stat().st_size,'sha256':sha(p),'category':relative.split('/')[0]})
(CHECK/'最终资料文件清单.json').write_text(json.dumps({'kind':'FINAL_DOCUMENT_FILES','date':'2026-10-03','entries':records,
    'excludes':'Advertising has separate manifests. Excludes self-reference, verification result, Word rendering/profile intermediates and temporary files.',
    'productInstallerCreated':False},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:report[k] for k in ['status','markdownCount','finalDocxCount','localLinksChecked','wordTextCharacters','wordTables','productAcceptance']},ensure_ascii=False))
