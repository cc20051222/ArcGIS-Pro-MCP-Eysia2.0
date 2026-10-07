from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re
from lxml import etree
import pypdfium2 as pdfium

ROOT=Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案\宣传\宣传图集')
QA=ROOT/'制作与核验'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
write=lambda p,v:p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
receipt=read(QA/'矢量图集制作回执.json')
specs=read(QA/'画廊浏览核验'/'文件与规格核验.json')
assert specs['pngCount']==specs['svgCount']==specs['pdfPageCount']==8
assert len(receipt['files'])==17
for entry in receipt['files']:
    assert sha(ROOT/entry['path'])==entry['sha256'],entry['path']
review=(QA/'宣传图集_独立视觉审读_20261003.md').read_text(encoding='utf-8')
assert '最终版复看' in review
review=review.split('最终 PNG 绑定快照')[-1]
for png in (ROOT/'01_PNG分享图').glob('*.png'):
    assert png.name in review and sha(png) in review
    assert any(x['sha256']==sha(png) for x in specs['pngs'])
for svg in (ROOT/'02_SVG可编辑图').glob('*.svg'):
    tree=etree.parse(str(svg))
    assert tree.getroot().get('viewBox')=='0 0 2400 3000'
    assert not tree.xpath('//*[local-name()="image"]')
    assert not tree.xpath('//*[local-name()="script"]')
    assert any(x['sha256']==sha(svg) for x in specs['svgs'])
pdfpath=ROOT/'03_整套图集'/'ArcGIS_Pro_MCP_3.0_宣传图集_矢量版.pdf'
doc=pdfium.PdfDocument(str(pdfpath)); assert len(doc)==8
for i,p in enumerate(doc):
    tp=p.get_textpage(); text=tp.get_text_range()
    assert '3.0 方案预告' in text and '实际验收为准' in text and '概念视觉' in text
    tp.close()
doc.close()
for d in receipt['designs']:d['visualReview']='ROOT_AND_INDEPENDENT_FINAL_PNG_REVIEW_COMPLETE'
receipt['status']='DOCUMENT_QA_COMPLETE_PASS_CANDIDATE'
receipt['completedUtc']=datetime.now(timezone.utc).isoformat()
receipt['finalReview']={'rootPagesViewed':list(range(1,9)),
                       'independentPagesViewed':list(range(1,9)),
                       'unresolvedVisualDefects':[],
                       'subjectiveAestheticApproval':'USER_REVIEW_OF_ARTWORK',
                       'browserReport':'制作与核验/画廊浏览核验/画廊浏览核验.json',
                       'productAcceptance':'NOT_RUN'}
receipt['revisions']=['Four distinct original cartographic geographies',
                      'Three-panel geographical foldout for business scene',
                      'Full chart and report composition for synchronized outputs',
                      'Lighter geographic layers with warm new top layer',
                      'Shorter paper shadows and increased footer clearance']
write(QA/'矢量图集制作回执.json',receipt)

copy=read(QA/'宣传图集_原生矢量实施文案.json')
copy['format'].update({'width':2400,'height':3000,'safeMargin':160})
copy['finalImplementation']='Original SVG/ReportLab cartographic illustration; 8-page 240x300mm PDF; PDFium PNG rendering. No image_gen used.'
write(QA/'宣传图集_原生矢量实施文案.json',copy)
public=[ROOT/'图集浏览.html',ROOT/'README_图集导航.md']+[ROOT/x['path'] for x in receipt['files']]
assert len(public)==19
manifest={'status':'DOCUMENT_QA_COMPLETE_PASS_CANDIDATE',
          'productAcceptance':'NOT_RUN','completedUtc':receipt['completedUtc'],
          'userSelectedMode':'D_DRIVE_ONLY_NATIVE_VECTOR',
          'files':[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in public]}
write(QA/'图集最终交付清单.json',manifest)

# Refresh only the documentation navigation hash in the earlier promotion list.
previous=QA.parent.parent/'制作与核验'/'宣传资料交付清单.json'
if previous.exists():
    old=read(previous)
    for entry in old['files']:
        p=previous.parent.parent/entry['path']
        if p.name=='README_宣传资料导航.md':
            entry.update({'bytes':p.stat().st_size,'sha256':sha(p)})
        else:assert sha(p)==entry['sha256']
    old['navigationUpdatedForAlbum']=receipt['completedUtc']
    write(previous,old)
print(json.dumps({'publicFiles':len(public),'png':8,'svg':8,'pdfPages':8,
                  'allEightRootAndIndependentReviewed':True,'pdfSha256':sha(pdfpath)},ensure_ascii=False))
