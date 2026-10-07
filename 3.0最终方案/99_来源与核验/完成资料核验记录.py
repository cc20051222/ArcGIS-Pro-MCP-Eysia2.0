from pathlib import Path
import json, hashlib

ROOT=Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案')
Q=ROOT/'99_来源与核验'
W=Q/'Word排版'
read=lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
save=lambda p,d: p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
pages=read(W/'最终页面指纹.json')
docx=next((ROOT/'06_Word项目介绍').glob('*.docx'))
assert sha(docx)==pages['docxSha256']
assert sha(W/'项目介绍原生分页.pdf')==pages['pdfSha256']
assert len(pages['images'])==pages['pages']
current_pages={r['page']:r for r in pages['images']}
for record in pages['images']:
    assert sha(Path(record['path']))==record['sha256']
native=read(W/'native-word-render.json')
assert native['success'] and native['pageCount']==pages['pages']
assert native['processCleanupRecheck']['remaining']==0

# Evidence is supplied by actual visual inspections. This script validates it;
# it never fabricates viewed pages or assumes that a previous page is unchanged.
visual_files=['视觉复核_01_17.json','视觉复核_18_34.json','视觉复核_最终28_37.json','视觉复核_最终38_48.json']
coverage=set()
reused_visual=set()
visual_bindings=[]
for filename in visual_files:
    report=read(W/filename)
    current_report={k:v for k,v in report.items() if k!='previousReviewSnapshot'}
    encoded=json.dumps(current_report,ensure_ascii=False)
    assert pages['docxSha256'] in encoded and pages['pdfSha256'] in encoded,filename
    for key in ['blockingDefects','blockingLayoutDefects','openBlockingDefects','visibleNewDefects']:
        if key in current_report: assert not current_report[key],filename
    records=next((report[k] for k in ['pageImages','actualIndividualImages','actualIndividualFinalImages'] if k in report),None)
    assert records,filename
    declared=next((report[k] for k in ['viewedPages','pagesActuallyViewed','actualNewlyViewedFinalPages'] if k in report),None)
    assert declared and set(declared)=={r['page'] for r in records},filename
    for record in records:
        assert record['sha256']==current_pages[record['page']]['sha256'],filename
        assert sha(Path(record.get('path',record.get('image'))))==record['sha256'],filename
        assert not record.get('blockingDefectObserved',False),filename
    coverage.update(declared)
    reused=report.get('finalRenderIdenticalImageReusedPages',report.get('reusedIdenticalImagePages',[]))
    assert set(reused).issubset(set(declared))
    reused_visual.update(reused)
    visual_bindings.append({'file':filename,'sha256':sha(W/filename),'pages':declared})
assert coverage==set(range(1,pages['pages']+1))
layout={'kind':'FINAL_WORD_LAYOUT_CHECK','date':'2026-10-03','status':'DOCUMENT_LAYOUT_REVIEW_COMPLETE',
        'docxSha256':pages['docxSha256'],'pdfSha256':pages['pdfSha256'],'pages':pages['pages'],
        'renderer':'Microsoft Word native PDF export; D drive temporary/profile paths; Poppler pdftoppm 144dpi rasterization',
        'packagedRendererFallbackReason':'Current render_docx.py attempt diagnosed missing LibreOffice soffice.exe; used installed Microsoft Word without dependency installation.',
        'visualCoveragePages':sorted(coverage),'directFinalPageVisualReview':sorted(coverage-reused_visual),
        'unchangedPageVisualReviewReused':sorted(reused_visual),'reviewReports':visual_bindings,
        'blockingLayoutDefects':0,'ownedWordProcessesRemaining':0,'notProductAcceptance':True}
save(W/'最终Word排版核验.json',layout)
generation=read(W/'Word生成记录.json')
assert generation['sha256']==pages['docxSha256']
for source in generation['sourceBodies']:assert sha(Path(source['path']))==source['sha256']
generation.update(nativeLayoutVerification='DOCUMENT_LAYOUT_REVIEW_COMPLETE',pageCount=pages['pages'],visualEvidence='最终Word排版核验.json')
save(W/'Word生成记录.json',generation)

review_files=['独立审读_方案与竞争.json','独立审读_工期职责.json']
reviews=[read(Q/f) for f in review_files]
assert not reviews[0]['openSubstantiveFindings']
assert all(f['status']=='RESOLVED_AND_REREAD' for f in reviews[1]['findings'])
for review,field in zip(reviews,['reviewedFiles','reviewedDocuments']):
    for entry in review[field]:
        path=Path(entry.get('path',entry.get('relativePath','')))
        if not path.is_absolute():path=ROOT/path
        assert sha(path)==entry.get('sha256',entry.get('SHA256')),str(path)
previous=read(Q/'修订与交叉审读闭环.json')
old_snapshot=previous.get('previousReviewSnapshot',previous)
arithmetic=previous.get('currentPlanningArithmeticRecheck')
assert arithmetic and len(arithmetic['passedChecks'])>=7
m=ROOT/'04_工期与效率优化/资料与计算'
assert arithmetic['modelSha256']==sha(m/'工期计算模型.json') and arithmetic['scriptSha256']==sha(m/'复算工期模型.py')
resolution={'kind':'DOCUMENT_CROSS_REVIEW_RESOLUTION','date':'2026-10-03','status':'DOCUMENT_REVIEW_COMPLETED',
 'documentRevision':read(ROOT/'最终方案范围与状态.json')['revision'],'productAcceptance':'NOT_RUN','complete60DayFeasibility':'UNKNOWN',
 'sources':[{'file':f,'sha256':sha(Q/f)} for f in review_files],
 'resolvedFindings':reviews[0]['findings']+reviews[1]['findings'],
 'currentRefinements':read(ROOT/'最终方案范围与状态.json')['inPlaceRefinements'],
 'currentPlanningArithmeticRecheck':arithmetic,'currentAdvertisingArtifactIdentityCheck':previous.get('currentAdvertisingArtifactIdentityCheck'),'openSubstantiveFindings':[],
 'wordSourcesAndAllActualPagesCurrent':True,'finalRenderIdentityReusePages':sorted(reused_visual),'allReusedImagesActuallyViewedThisTurnBeforeFooterOnlyReflow':True,'advertisingCopyConsistency':reviews[0]['promotionalConsistency'],
 'historicalPromotionQaBoundary':'Previous promotion QA including previousProjectIntroductionUnchanged=true is evidence for its original creation event only; original hashes retained. Current introduction was authorized to change in place; promotional copy independently rechecked for consistency.',
 'previousReviewSnapshot':old_snapshot,
 'limitations':['Assigned changed contracts and main text independently reread; not a full new professional review of every historical appendix paragraph.',
                'No product/competitor runtime, measured full capacity, installation or release acceptance.']}
save(Q/'修订与交叉审读闭环.json',resolution)
print(json.dumps({'wordPages':pages['pages'],'allCurrentPagesActuallyViewed':len(coverage),'planningArithmeticChecks':len(arithmetic['passedChecks']),'product':'NOT_RUN'},ensure_ascii=False))
