"""Create editable vector posters and a two-page print PDF. All writes stay on D."""
from pathlib import Path
import json, math, hashlib, html
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.colors import HexColor
import pypdfium2 as pdfium

ROOT = Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案\宣传')
OUT = ROOT / '海报'
QA = ROOT / '制作与核验'
OUT.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont('PromoRegular', r'C:\Windows\Fonts\msyh.ttc', subfontIndex=0))
pdfmetrics.registerFont(TTFont('PromoBold', r'C:\Windows\Fonts\msyhbd.ttc', subfontIndex=0))
COPY_PATH = QA / '宣传文案.json'
COPY = json.loads(COPY_PATH.read_text(encoding='utf-8-sig'))
INK, TEAL, MINT, PAPER, AMBER = '#082F33', '#0D8A80', '#A9E1D1', '#F8F6EF', '#B87136'
WHITE, SOFT, GREY = '#FFFFFF', '#DCF0E8', '#50686B'
PDF_PATH = OUT / 'ArcGIS_Pro_MCP_3.0_宣传海报_打印版.pdf'
pdf = canvas.Canvas(str(PDF_PATH), pagesize=(841.89, 1190.55), pageCompression=1)
pdf.setTitle('ArcGIS Pro MCP 3.0 产品预告宣传海报')
pdf.setAuthor('ArcGIS Pro MCP 项目宣传资料')
pdf.setSubject('3.0 规划功能与目标体验 概念视觉 非真实分析结果')
checks = []

class Art:
    def __init__(self, width, height, physical_width, name):
        self.w, self.h, self.name = width, height, name
        self.scale = physical_width / width
        pdf.setPageSize((physical_width, height * self.scale))
        pdf.saveState()
        pdf.scale(self.scale, self.scale)
        self.svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img">',
                    '<title>ArcGIS Pro MCP 3.0 方案预告宣传海报</title>',
                    '<desc>概念宣传视觉。规划功能与目标体验以实际验收为准。</desc>']
        self.texts = []

    def rect(self, x, y, w, h, fill, stroke=None, sw=1):
        pdf.setFillColor(HexColor(fill))
        if stroke:
            pdf.setStrokeColor(HexColor(stroke)); pdf.setLineWidth(sw)
        pdf.rect(x, self.h-y-h, w, h, stroke=bool(stroke), fill=1)
        self.svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')

    def line(self, points, color, sw=3):
        pdf.setStrokeColor(HexColor(color)); pdf.setLineWidth(sw)
        pdf.setLineCap(1); pdf.setLineJoin(1)
        p = pdf.beginPath(); p.moveTo(points[0][0], self.h-points[0][1])
        for x,y in points[1:]: p.lineTo(x,self.h-y)
        pdf.drawPath(p, fill=0, stroke=1)
        coords = ' '.join(f'{x},{y}' for x,y in points)
        self.svg.append(f'<polyline points="{coords}" fill="none" stroke="{color}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"/>')

    def circle(self, x, y, radius, fill, stroke=None, sw=2):
        pdf.setFillColor(HexColor(fill))
        if stroke: pdf.setStrokeColor(HexColor(stroke)); pdf.setLineWidth(sw)
        pdf.circle(x,self.h-y,radius,stroke=bool(stroke),fill=1)
        self.svg.append(f'<circle cx="{x}" cy="{y}" r="{radius}" fill="{fill}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')

    def polygon(self, points, fill, stroke=None, sw=2):
        pdf.setFillColor(HexColor(fill))
        if stroke: pdf.setStrokeColor(HexColor(stroke)); pdf.setLineWidth(sw)
        p=pdf.beginPath(); p.moveTo(points[0][0],self.h-points[0][1])
        for x,y in points[1:]: p.lineTo(x,self.h-y)
        p.close(); pdf.drawPath(p,fill=1,stroke=bool(stroke))
        coords=' '.join(f'{x},{y}' for x,y in points)
        self.svg.append(f'<polygon points="{coords}" fill="{fill}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')

    def text(self, x, baseline, value, size, color=INK, bold=False, max_width=None):
        font='PromoBold' if bold else 'PromoRegular'
        measured=pdfmetrics.stringWidth(value,font,size)
        if max_width is not None and measured > max_width:
            raise ValueError(f'{self.name}: text wider than allocated width: {value} {measured} > {max_width}')
        if x+measured > self.w-55 or baseline > self.h-30 or baseline-size < 0:
            raise ValueError(f'{self.name}: text outside page: {value}')
        pdf.setFillColor(HexColor(color)); pdf.setFont(font,size)
        pdf.drawString(x,self.h-baseline,value)
        self.svg.append(f'<text x="{x}" y="{baseline}" font-family="Microsoft YaHei, Noto Sans CJK SC, sans-serif" font-size="{size}" font-weight="{700 if bold else 400}" fill="{color}">{html.escape(value)}</text>')
        self.texts.append({'text':value,'x':x,'baseline':baseline,'size':size,'width':round(measured,2)})

    def finish(self):
        self.svg.append('</svg>')
        (OUT/(self.name+'.svg')).write_text('\n'.join(self.svg),encoding='utf-8')
        pdf.restoreState(); pdf.showPage()
        checks.append({'poster':self.name,'pixelSize':[self.w,self.h],
                       'pdfPoints':[round(self.w*self.scale,2),round(self.h*self.scale,2)],
                       'textPlacement':'PASS','textElements':self.texts})

def draw_map(a,x,y,w,h):
    a.rect(x,y,w,h,SOFT)
    for r in range(8):
        pts=[]
        for j in range(30):
            t=j/29
            yy=y+h*(.12+.10*r)+math.sin(t*8+r*.44)*h*.055
            pts.append((x+w*t, yy))
        a.line(pts,'#BCD8CF',max(2,w*.0026))
    # Neutral abstract geometry; it is explicitly a concept graphic.
    tiles=[(.12,.22,.15,.13),(.32,.12,.18,.22),(.55,.27,.14,.17),(.74,.12,.14,.22),
           (.18,.56,.18,.20),(.44,.64,.14,.17),(.66,.61,.20,.19)]
    for tx,ty,tw,th in tiles:
        a.polygon([(x+w*tx,y+h*ty),(x+w*(tx+tw),y+h*(ty+.025)),
                   (x+w*(tx+tw-.018),y+h*(ty+th)),(x+w*(tx-.012),y+h*(ty+th-.02))],
                  '#C6E4D7', '#98C6B5', max(1.5,w*.0015))
    river=[(x+w*t,y+h*(.16+.7*t+.1*math.sin(t*9))) for t in [j/34 for j in range(35)]]
    a.line(river, '#91CEC7', w*.048)
    a.line(river, '#D7ECE7', w*.021)
    route=[(x+w*.07,y+h*.66),(x+w*.25,y+h*.44),(x+w*.48,y+h*.48),
           (x+w*.65,y+h*.30),(x+w*.91,y+h*.37)]
    a.line(route, AMBER, w*.012)
    for px,py in route[1:-1]:
        a.circle(px,py,w*.022,WHITE,AMBER,max(2,w*.005))
    a.line([(x+w*.14,y+h*.90),(x+w*.14,y+h*.76)],TEAL,max(3,w*.004))
    a.polygon([(x+w*.14,y+h*.74),(x+w*.125,y+h*.785),(x+w*.155,y+h*.785)],TEAL)

def draw_design(a,x,y,w,h):
    a.rect(x,y,w,h,WHITE)
    a.rect(x+w*.075,y+h*.075,w*.85,h*.11,TEAL)
    a.line([(x+w*.13,y+h*.126),(x+w*.60,y+h*.126)],MINT,w*.009)
    draw_map(a,x+w*.075,y+h*.22,w*.85,h*.49)
    for i,width in enumerate([.54,.77,.63]):
        a.line([(x+w*.09,y+h*(.78+.055*i)),(x+w*(.09+width),y+h*(.78+.055*i))],
               '#B4C8C3',w*.010)
    a.rect(x+w*.075,y+h*.93,w*.24,h*.027,AMBER)

def draw_office(a,x,y,w,h):
    a.rect(x,y,w,h,WHITE)
    a.rect(x+w*.09,y+h*.09,w*.13,h*.10,TEAL)
    for i,width in enumerate([.53,.64]):
        a.line([(x+w*.29,y+h*(.116+.06*i)),(x+w*(.29+width),y+h*(.116+.06*i))],
               '#ABBDB8',w*.014)
    for i in range(4):
        a.line([(x+w*.09,y+h*(.27+.045*i)),(x+w*.91,y+h*(.27+.045*i))],
               '#CEDAD4',w*.007)
    a.line([(x+w*.12,y+h*.68),(x+w*.12,y+h*.48)],'#8EAAA1',w*.005)
    a.line([(x+w*.12,y+h*.68),(x+w*.86,y+h*.68)],'#8EAAA1',w*.005)
    for i,bh in enumerate([.10,.15,.12,.21]):
        a.rect(x+w*(.20+.15*i),y+h*(.68-bh),w*.083,h*bh,TEAL if i%2 else '#86BDB0')
    for i,width in enumerate([.77,.62,.78,.58]):
        a.line([(x+w*.09,y+h*(.77+.048*i)),(x+w*(.09+width),y+h*(.77+.048*i))],
               '#CEDAD4',w*.009)

def portrait():
    p=COPY['twoPoster'][0]
    a=Art(3000,4243,841.89,'01_主宣传海报_竖版')
    a.rect(0,0,3000,4243,PAPER)
    a.rect(0,0,3000,28,TEAL)
    a.text(218,187,'产品愿景 / PRODUCT PREVIEW',42,GREY,max_width=1500)
    a.text(2340,187,p['status'],46,TEAL,True,max_width=460)
    a.text(218,377,p['brand'],96,INK,True,max_width=2550)
    a.text(212,657,'让 GIS 成果',167,INK,True,max_width=2570)
    a.text(212,856,'从分析走向交付',167,TEAL,True,max_width=2570)
    a.text(218,1040,p['subtitle'],58,GREY,max_width=2560)
    a.line([(218,1145),(2782,1145)],'#BFCFC5',3)
    # Editorial illustration built from three document types, never a fake UI.
    a.rect(218,1260,2564,1535,INK)
    a.text(322,1400,'同一任务  ·  同一事实  ·  同一成果版本',44,MINT,max_width=2270)
    a.rect(325,1575,1310,928,'#146363')
    draw_map(a,355,1605,1250,868)
    a.text(383,1535,'GIS',64,WHITE,True,max_width=550)
    a.text(2020,1535,'Office',62,WHITE,True,max_width=610)
    draw_office(a,1930,1605,665,1058)
    a.rect(1057,1806,863,1055,'#0B4B4F')
    draw_design(a,1090,1770,800,1010)
    a.rect(1072,1640,840,110,INK)
    a.text(1133,1726,'Photoshop',58,WHITE,True,max_width=750)
    a.line([(758,2640),(980,2640),(1015,2620)],MINT,5)
    a.line([(1780,2715),(1870,2715),(1898,2695)],MINT,5)
    a.text(222,2910,'概念示意 · 非真实分析结果',39,GREY,max_width=2520)
    a.text(218,3024,'GIS → Photoshop → Office',62,TEAL,True,max_width=2560)
    for i,item in enumerate(p['sellingPoints']):
        yy=3194+i*170
        a.circle(242,yy-22,11,TEAL)
        a.text(292,yy,item,60,INK,bold=(i==0),max_width=2450)
    a.line([(218,3850),(2782,3850)],'#BFCFC5',3)
    a.text(218,3970,p['cta'],48,TEAL,True,max_width=2560)
    a.text(218,4088,'3.0 规划功能与目标体验；发布功能及支持条件以实际验收为准。',39,GREY,max_width=2560)
    a.text(218,4160,'概念宣传视觉  ·  ArcGIS Pro MCP 3.0 产品方案预告',37,GREY,max_width=2560)
    a.finish()

def landscape():
    p=COPY['twoPoster'][1]
    a=Art(3840,2160,1190.55,'02_产品亮点海报_横版')
    a.rect(0,0,3840,2160,PAPER)
    a.rect(0,0,3840,26,TEAL)
    a.text(190,198,p['brand'],84,INK,True,max_width=2510)
    a.text(3290,190,p['status'],46,TEAL,True,max_width=390)
    a.text(183,453,'一条成果链',132,INK,True,max_width=2250)
    a.text(183,627,'连接你的 GIS 工作',132,TEAL,True,max_width=2250)
    a.text(190,795,p['subtitle'],61,GREY,max_width=2100)
    for i,item in enumerate(p['sellingPoints']):
        yy=1015+i*171
        label,detail=item.split('：',1)
        a.text(190,yy,label,64,TEAL,True,max_width=480)
        a.text(485,yy,detail,56,INK,max_width=1730)
    a.text(190,1785,p['chain'],84,TEAL,True,max_width=2100)
    a.text(190,1908,p['cta'],48,INK,True,max_width=2100)
    a.rect(2355,340,1295,1465,INK)
    a.text(2445,457,'同一任务 · 同一成果版本',43,MINT,max_width=1120)
    a.text(2450,584,'GIS',54,WHITE,True,max_width=600)
    draw_map(a,2448,628,940,605)
    a.text(3120,1668,'Office',45,WHITE,True,max_width=430)
    draw_office(a,3000,1148,540,458)
    a.rect(2408,940,650,748,'#0B4B4F')
    draw_design(a,2448,916,560,711)
    a.rect(2428,808,605,93,INK)
    a.text(2490,878,'Photoshop',46,WHITE,True,max_width=550)
    a.text(2395,1751,'概念示意 · 非真实分析结果',37,MINT,max_width=1200)
    a.line([(190,1981),(3650,1981)],'#BFCFC5',3)
    a.text(190,2060,'3.0 规划功能与目标体验；发布功能及支持条件以实际验收为准。',40,GREY,max_width=3440)
    a.text(2690,2060,'概念宣传视觉',40,GREY,max_width=950)
    a.finish()

portrait(); landscape(); pdf.save()
doc=pdfium.PdfDocument(str(PDF_PATH))
for index,item in enumerate(checks):
    page=doc[index]
    target_width=item['pixelSize'][0]
    img=page.render(scale=target_width/page.get_width()).to_pil()
    # PDFium may round the mathematical page size upward by a pixel. Use the
    # exact vector viewport ratio for a predictable sharing canvas.
    expected=tuple(item['pixelSize'])
    if img.size != expected:
        img=img.resize(expected)
    dpi=72*target_width/item['pdfPoints'][0]
    img.save(OUT/(item['poster']+'.png'),dpi=(dpi,dpi))
    item['renderedPixels']=list(img.size)
doc.close()
(QA/'海报制作与文字边界核验.json').write_text(json.dumps({
    'method':'Native editable SVG and ReportLab vector PDF; PDFium rasterization',
    'imagegenCalled':False,'copySha256':hashlib.sha256(COPY_PATH.read_bytes()).hexdigest().upper(),
    'status':'DOCUMENT QA ONLY / NOT PRODUCT ACCEPTANCE',
    'fonts':'Microsoft YaHei document subset embedded in PDF; system font referenced by SVG',
    'posters':checks,
    'files':[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,
              'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sorted(OUT.iterdir()) if p.is_file()]
},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'pdfPages':2,'posters':[x['poster'] for x in checks],
                  'path':str(OUT)},ensure_ascii=False))
