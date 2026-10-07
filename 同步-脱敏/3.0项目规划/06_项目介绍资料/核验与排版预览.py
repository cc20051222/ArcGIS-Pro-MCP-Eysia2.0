"""Read the actual DOCX for an approximate OOXML-layout preview.
This is not native Word pagination verification; that limitation stays explicit.
"""
from pathlib import Path
from io import BytesIO
import re
import json
import hashlib
import html
import subprocess
from docx import Document
from docx.text.paragraph import Paragraph as DocParagraph
from docx.table import Table as DocTable
from docx.oxml.ns import qn
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT,TA_CENTER,TA_JUSTIFY
from reportlab.lib.units import cm
from reportlab.lib.pagesizes import A4
from reportlab.platypus import SimpleDocTemplate, Paragraph, Table, TableStyle, Image, PageBreak, KeepTogether
from pypdf import PdfReader

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
QA=HERE/'qa'
OUT=QA/'rendered'
OUT.mkdir(parents=True,exist_ok=True)
DOCX=ROOT/'ArcGIS_Pro_MCP_3.0_项目介绍与实现方案_详细版_20261001.docx'
PDF=QA/'OOXML排版检查预览.pdf'
POPPLER=Path('<user-home>/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin')
pdfmetrics.registerFont(TTFont('YaHei','C:/Windows/Fonts/msyh.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('YaHeiBold','C:/Windows/Fonts/msyhbd.ttc',subfontIndex=0))
pdfmetrics.registerFontFamily('YaHei',normal='YaHei',bold='YaHeiBold',italic='YaHei',boldItalic='YaHeiBold')

styles={
'Normal':ParagraphStyle('body',fontName='YaHei',fontSize=11,leading=14.3,wordWrap='CJK',alignment=TA_JUSTIFY,spaceAfter=7,firstLineIndent=.73*cm,allowWidows=0,allowOrphans=0),
'Title':ParagraphStyle('title',fontName='YaHeiBold',fontSize=27,leading=36,spaceAfter=16,wordWrap='CJK'),
'Subtitle':ParagraphStyle('subtitle',fontName='YaHei',fontSize=16,leading=22,spaceAfter=15,wordWrap='CJK'),
'Heading 1':ParagraphStyle('h1',fontName='YaHeiBold',fontSize=17,leading=23,spaceBefore=17,spaceAfter=7,keepWithNext=True,wordWrap='CJK'),
'Heading 2':ParagraphStyle('h2',fontName='YaHeiBold',fontSize=12.5,leading=18,spaceBefore=10,spaceAfter=7,keepWithNext=True,wordWrap='CJK'),
'Caption':ParagraphStyle('caption',fontName='YaHei',fontSize=9.5,leading=13,spaceAfter=8,alignment=TA_CENTER,wordWrap='CJK'),
'Cell':ParagraphStyle('cell',fontName='YaHei',fontSize=9.5,leading=10.9,wordWrap='CJK',spaceAfter=1,spaceBefore=1),
'Header':ParagraphStyle('header',fontName='YaHeiBold',fontSize=9.5,leading=11.2,wordWrap='CJK',alignment=TA_CENTER,textColor=colors.white),
}
doc=Document(DOCX)
flow=[];source_text=[];heading_text=[];tables=0;figures=0
blocks=list(doc.element.body)
def escape(text):
    return html.escape(text).replace('\n','<br/>')
for elem in blocks:
    if elem.tag==qn('w:p'):
        p=DocParagraph(elem,doc)
        text=p.text
        if p._p.xpath('.//w:br[@w:type="page"]'):
            flow.append(PageBreak())
            continue
        blips=p._p.xpath('.//a:blip')
        if blips:
            for blip in blips:
                rid=blip.get(qn('r:embed'))
                data=doc.part.related_parts[rid].blob
                extent=p._p.xpath('.//wp:extent')[0]
                width=int(extent.get('cx'))/12700
                height=int(extent.get('cy'))/12700
                flow.append(Image(BytesIO(data),width=width,height=height))
                figures+=1
            continue
        if not text.strip():
            continue
        source_text.append(text)
        name=p.style.name
        if name=='Heading 1':heading_text.append(text)
        sty=ParagraphStyle('copy',parent=styles.get(name,styles['Normal']))
        pf=p.paragraph_format
        if pf.first_line_indent is not None:sty.firstLineIndent=pf.first_line_indent.pt
        if pf.space_after is not None:sty.spaceAfter=pf.space_after.pt
        if pf.line_spacing and isinstance(pf.line_spacing,float):sty.leading=sty.fontSize*pf.line_spacing
        if pf.keep_with_next:sty.keepWithNext=True
        if p.alignment==1:sty.alignment=TA_CENTER
        elif p.alignment==0:sty.alignment=TA_LEFT
        # Directly formatted hyperlinks in TOC/bibliography carry their own size.
        sizes=[int(x.get(qn('w:val')))/2 for x in p._p.xpath('.//w:sz')]
        if sizes and name=='Normal':
            sty.fontSize=min(sizes);sty.leading=sty.fontSize*1.3
        flow.append(Paragraph(escape(text),sty))
    elif elem.tag==qn('w:tbl'):
        t=DocTable(elem,doc);tables+=1
        widths=[col.width.pt for col in t.columns]
        data=[]
        for rownum,row in enumerate(t.rows):
            values=[]
            for colnum,cell in enumerate(row.cells):
                tx='\n'.join(p.text for p in cell.paragraphs)
                source_text.append(tx)
                sty=ParagraphStyle('copycell',parent=styles['Header'] if rownum==0 else styles['Cell'])
                if rownum and colnum==0 and len(tx)<=22:sty.alignment=TA_CENTER
                values.append(Paragraph(escape(tx),sty))
            data.append(values)
        table=Table(data,colWidths=widths,repeatRows=1,hAlign='CENTER',splitByRow=1)
        commands=[('GRID',(0,0),(-1,-1),.45,colors.HexColor('#D9D9D9')),('BACKGROUND',(0,0),(-1,0),colors.HexColor('#243A51')),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),6),('RIGHTPADDING',(0,0),(-1,-1),6),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),5),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#F0F4F8')])]
        table.setStyle(TableStyle(commands));flow.append(table)

def page_footer(canvas,docobj):
    canvas.saveState();canvas.setFont('YaHei',9);canvas.setFillColor(colors.black)
    canvas.drawCentredString(A4[0]/2,.9*cm,'第 '+str(docobj.page)+' 页')
    canvas.restoreState()

pdf=SimpleDocTemplate(str(PDF),pagesize=A4,leftMargin=2.15*cm,rightMargin=2.05*cm,topMargin=2.05*cm,bottomMargin=1.95*cm,title='ArcGIS Pro MCP 3.0 项目介绍 排版检查预览',author='项目规划资料')
pdf.build(flow,onFirstPage=page_footer,onLaterPages=page_footer)
reader=PdfReader(str(PDF))
all_text=''.join(p.extract_text() or '' for p in reader.pages)
norm=lambda x:re.sub(r'\s+','',x)
missing=[title for title in heading_text if norm(title) not in norm(all_text)]
if missing:raise RuntimeError('Missing headings in layout preview: '+str(missing))
subprocess.run([str(POPPLER/'pdftoppm.exe'),'-png','-r','130',str(PDF),str(OUT/'page')],check=True,cwd=str(QA))
for path in list(OUT.glob('page-*.png')):
    n=int(path.stem.split('-')[-1])
    target=OUT/f'page-{n}.png'
    if target!=path:path.replace(target)
manifest=json.loads((QA/'生成与核验清单.json').read_text(encoding='utf-8'))
manifest.update({'docxBytes':DOCX.stat().st_size,'sha256':hashlib.sha256(DOCX.read_bytes()).hexdigest().upper(),'layoutStatus':'APPROXIMATE_OOXML_PREVIEW_RENDERED_AWAITING_IMAGE_REVIEW','nativeWordPaginationVerified':False,'nativeRendererLimit':'Bundled Windows runtime has no LibreOffice; native Office COM activation failed 0x80070520. No dependency installed and no application settings changed.','previewMethod':'Actual DOCX paragraphs tables embedded figures and styles read into bundled ReportLab; this verifies approximation only, not native Word pagination.','previewPages':len(reader.pages),'tablesParsed':tables,'figuresParsed':figures,'missingSectionHeadings':missing,'pngPages':len(list(OUT.glob('page-*.png'))),'competitorSourceStatus':'Public primary-source claims checked 2026-10-01; no competitor runtime tests','visualInspection':'PENDING'})
(QA/'生成与核验清单.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'pages':len(reader.pages),'tables':tables,'figures':figures,'pngPages':manifest['pngPages'],'nativeWordPaginationVerified':False,'missingHeadings':missing},ensure_ascii=False))
