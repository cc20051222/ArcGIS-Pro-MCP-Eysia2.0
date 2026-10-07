from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re

ROOT=Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案\宣传')
QA=ROOT/'制作与核验'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
write=lambda p,v:p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
stamp=datetime.now(timezone.utc).isoformat()

qa=read(QA/'宣传资料交付核验.json')
qa['status']='DOCUMENT_QA_COMPLETE_PASS_CANDIDATE'
qa['completedUtc']=stamp
qa['visualReview']={
    'reviewer':'Root documentation author',
    'method':'Viewed actual Microsoft Word PDF renders of all 12 pages, plus both final poster PNGs rasterized from print PDF',
    'brochurePagesViewed':list(range(1,11)),
    'posterWordPagesViewed':[1,2],'printPosterPagesViewed':[1,2],
    'findings':['Chinese readable without missing glyphs',
                'No heading or paragraph overflow; no clipped poster; original aspect ratios retained',
                'All pages carry preview and planning qualification; no empty extra pages',
                'Photoshop and Office illustration labels fully readable after drawing-order correction'],
    'unresolvedFindings':[]}
for d in qa['documents']:
    d['visualReview']='COMPLETE_ALL_NATIVE_PAGES_VIEWED'
    assert sha(ROOT/d['document'])==d['sha256']
    label='宣传册原生渲染' if d['nativePages']==10 else '海报Word原生渲染'
    r=read(QA/'临时'/label/'native-word-render.json')
    assert r['success'] and r['cleanupRemaining']==0
qa['nativeWordCleanupRemaining']=0
qa['fallbackRendererNote']='Bundled render_docx.py was attempted; LibreOffice was unavailable. Existing Microsoft Word 16 performed native read-only PDF export; PDFium rendered actual page PNGs. No software installed.'
independent=read(QA/'最终宣传资料独立审读.json')
qa['independentReviewContext']={
    'path':'制作与核验/最终宣传资料独立审读.json',
    'reviewKind':independent['reviewKind'],
    'substantiveFindings':independent['substantiveFindings'],
    'posterArtifactsUnchangedSinceIndependentReview':all(sha(Path(x['path']))==x['sha256'] for x in independent['posters']),
    'wordReviewedBaseAndFinal':[{'path':str(Path(x['path']).relative_to(ROOT)),
                                'independentBaseSha256':x['sha256'],
                                'finalSha256':sha(Path(x['path'])),
                                'delta':'Word headings stripped of punctuation and brochure closing slogans removed; no new substantive claim. Final text and native pages re-read by root.'}
                               for x in independent['wordDocuments']],
    'rootFinalTextReviewComplete':True}

nav=ROOT/'README_宣传资料导航.md'
targets=re.findall(r'\]\(<([^>]+)>\)',nav.read_text(encoding='utf-8'))
missing=[p for p in targets if not Path(p).exists()]
assert not missing,missing
qa['navigationChecks']={'targets':len(targets),'missing':missing}
old_intro=Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案\06_Word项目介绍\ArcGIS_Pro_MCP_3.0_项目介绍与完整实现方案_最终版_20261002.docx')
expected_old='E886974661665BA28582A58339038B806D92C86788EA9757D15907F9032580F9'
assert sha(old_intro)==expected_old
qa['previousProjectIntroductionUnchanged']=True
write(QA/'宣传资料交付核验.json',qa)
author=read(QA/'宣传Word制作回执.json')
author['finalRootReview']={'completedUtc':stamp,'nativePages':[10,2],
                          'all12PagesViewed':True,'status':'DOCUMENT_QA_COMPLETE_PASS_CANDIDATE',
                          'productAcceptance':'NOT_RUN'}
write(QA/'宣传Word制作回执.json',author)

public=[nav]+sorted((ROOT/'Word').glob('*.docx'))+sorted(p for p in (ROOT/'海报').iterdir() if p.suffix in {'.png','.svg','.pdf'})
assert len(public)==8
manifest={'edition':'3.0 方案预告','completedUtc':stamp,
          'status':'DOCUMENT_QA_COMPLETE_PASS_CANDIDATE','productAcceptance':'NOT_RUN',
          'root':str(ROOT),'wordDocuments':2,'posterDesigns':2,'printPdfPages':2,
          'files':[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in public],
          'notes':['Public document generation only; no product implementation, installation or external publication.',
                   'All authoring and temporary artifacts were directed to the D-drive project directory.',
                   'Word poster pages contain image displays; editable vector sources are the SVGs.']}
write(QA/'宣传资料交付清单.json',manifest)
print(json.dumps({'publicFiles':len(public),'nativeWordPages':[10,2],
                  'allNativePagesVisuallyViewed':True,'navigationTargets':len(targets),
                  'missingLinks':0,'remainingOwnedWordProcesses':0},ensure_ascii=False))
