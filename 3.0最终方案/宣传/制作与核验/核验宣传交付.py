from pathlib import Path
import hashlib, json, re
from zipfile import ZipFile
from lxml import etree
import pypdfium2 as pdfium
from PIL import Image

ROOT = Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案\宣传')
QA = ROOT / '制作与核验'
WNS={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
cases=[('宣传册', 'ArcGIS_Pro_MCP_3.0_产品宣传册_预告版.docx', '宣传册原生渲染', '宣传册.pdf',10),
       ('海报Word','ArcGIS_Pro_MCP_3.0_宣传海报_展示版.docx','海报Word原生渲染','海报Word.pdf',2)]
reports=[]
for label,name,dirname,pdfname,expected in cases:
    path=ROOT/'Word'/name
    renderdir=QA/'临时'/dirname
    doc=pdfium.PdfDocument(str(renderdir/pdfname))
    assert len(doc)==expected,(label,len(doc),expected)
    textpages=[]
    for i,page in enumerate(doc):
        textpage=page.get_textpage()
        text=textpage.get_text_range()
        textpages.append(text)
        assert '3.0 方案预告' in text, (label,i+1,'missing preview label')
        assert '实际验收为准' in text, (label,i+1,'missing boundary')
        assert '\ufffd' not in text,(label,i+1,'replacement glyph')
        page.render(scale=1.65).to_pil().save(renderdir/f'page-{i+1:02}.png')
        textpage.close()
    doc.close()
    (renderdir/'page-text.json').write_text(json.dumps(textpages,ensure_ascii=False,indent=2),encoding='utf-8')
    with ZipFile(path) as z:
        tree=etree.fromstring(z.read('word/document.xml'))
        heading_checks=[]
        for paragraph in tree.xpath('//w:p',namespaces=WNS):
            styles=paragraph.xpath('./w:pPr/w:pStyle/@w:val',namespaces=WNS)
            text=''.join(paragraph.xpath('.//w:t/text()',namespaces=WNS))
            if any(s.startswith('Heading') or s=='Title' for s in styles):
                assert not re.search(r'[，。、：；！？,.!?;:]',text),(label,'heading punctuation',text)
                heading_checks.append(text)
        images=[z.read(n) for n in z.namelist() if n.startswith('word/media/')]
        expected_image_hashes={sha(p) for p in (ROOT/'海报').glob('*.png')}
        assert {hashlib.sha256(x).hexdigest().upper() for x in images}==expected_image_hashes
    reports.append({'document':str(path.relative_to(ROOT)),'bytes':path.stat().st_size,
                    'sha256':sha(path),'nativePages':expected,'everyPagePreviewAndBoundary':True,
                    'embeddedImagesMatchFinalPosters':True,'headingStyleChecks':heading_checks,
                    'visualReview':'PENDING_ACTUAL_PAGE_REVIEW'})

poster_pdf=ROOT/'海报'/'ArcGIS_Pro_MCP_3.0_宣传海报_打印版.pdf'
doc=pdfium.PdfDocument(str(poster_pdf))
assert len(doc)==2
poster_pages=[]
for i,page in enumerate(doc):
    tp=page.get_textpage(); text=tp.get_text_range()
    assert '3.0 方案预告' in text and '实际验收为准' in text
    assert '概念示意' in text and '非真实分析结果' in text
    assert all(s in text for s in ['GIS','Photoshop','Office'])
    poster_pages.append({'page':i+1,'points':list(page.get_size()),'text':text})
    tp.close()
doc.close()

png_reports=[]
for name,dimensions in [('01_主宣传海报_竖版.png',(3000,4243)),('02_产品亮点海报_横版.png',(3840,2160))]:
    p=ROOT/'海报'/name
    with Image.open(p) as im:
        assert im.size==dimensions
        png_reports.append({'file':name,'pixels':list(im.size),'dpiMetadata':im.info.get('dpi')})

report={'status':'STRUCTURE_AND_NATIVE_RENDER_CHECKS_PASSED_VISUAL_REVIEW_PENDING',
        'scope':'Document QA only; not product capability acceptance',
        'documents':reports,'posterPdf':{'sha256':sha(poster_pdf),'pages':poster_pages},
        'png':png_reports,'noDependenciesInstalled':True,
        'projectOutputRoot':str(ROOT),'productAcceptance':'NOT_RUN'}
(QA/'宣传资料交付核验.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'wordPages':[r['nativePages'] for r in reports],'posterPdfPages':2,
                  'pngDimensions':[r['pixels'] for r in png_reports]},ensure_ascii=False))
