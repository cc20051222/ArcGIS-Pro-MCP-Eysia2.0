"""Original cartographic campaign art. Vector geometry only; all outputs on D."""
from pathlib import Path
import math, json, random, hashlib, html
from contextlib import contextmanager
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.colors import HexColor
import pypdfium2 as pdfium

ROOT=Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案\宣传\宣传图集')
QA=ROOT/'制作与核验'
PNG=ROOT/'01_PNG分享图'; SVG=ROOT/'02_SVG可编辑图'; BOOK=ROOT/'03_整套图集'
for d in [QA,PNG,SVG,BOOK,QA/'临时']: d.mkdir(parents=True,exist_ok=True)
COPY=json.loads((QA/'宣传图集_原生矢量实施文案.json').read_text(encoding='utf-8-sig'))
W,H=2400,3000
POINT_W=680.315; POINT_H=850.394
NAMES=['01_全链路主视觉','02_自然语言任务','03_PS自动成图','04_参考风格学习',
       '05_GP持续适配','06_成果同步更新','07_多业务场景','08_完整成果交付']
INK='#122B3A'; PAPER='#F5F1E9'; TEAL='#167A79'; CORAL='#D97958'
pdfmetrics.registerFont(TTFont('ArtRegular',r'C:\Windows\Fonts\msyh.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('ArtBold',r'C:\Windows\Fonts\msyhbd.ttc',subfontIndex=0))
PDF_PATH=BOOK/'ArcGIS_Pro_MCP_3.0_宣传图集_矢量版.pdf'
PDF=canvas.Canvas(str(PDF_PATH),pagesize=(POINT_W,POINT_H),pageCompression=1)
PDF.setTitle('ArcGIS Pro MCP 3.0 原生矢量宣传图集')
PDF.setAuthor('ArcGIS Pro MCP 3.0 项目宣传资料')
PDF.setSubject('Eight original vector campaign illustrations. Concept visuals; planned product experience.')

def rgb(s): return tuple(int(s[i:i+2],16)/255 for i in (1,3,5))
def mix(a,b,t):
    ra,rb=rgb(a),rgb(b)
    return '#'+''.join(f'{round(255*(u*(1-t)+v*t)):02X}' for u,v in zip(ra,rb))
def darken(color,t): return mix(color,'#182331',t)

class Art:
    def __init__(self,name,index,dark=False):
        self.name=name; self.index=index; self.dark=dark; self.texts=[]; self.clip_id=0; self.elements=0; self.opacity=1
        self.bg='#101F31' if dark else PAPER
        self.fg='#F4F3EA' if dark else INK
        self.accent='#7BE0D0' if dark else TEAL
        self.stroke='#355268' if dark else '#D0D9D2'
        PDF.saveState(); PDF.scale(POINT_W/W,POINT_H/H)
        self.svg=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="img">',
                  '<title>ArcGIS Pro MCP 3.0 原生矢量宣传图集</title>',
                  '<desc>原创地理概念插画。3.0方案预告，规划功能及支持条件以实际验收为准。</desc>']
        self.rect(0,0,W,H,self.bg)

    @contextmanager
    def alpha(self,opacity):
        old_alpha=self.opacity;self.opacity*=opacity
        PDF.saveState(); PDF.setFillAlpha(self.opacity); PDF.setStrokeAlpha(self.opacity)
        self.svg.append(f'<g opacity="{opacity}">')
        yield
        self.svg.append('</g>'); PDF.restoreState();self.opacity=old_alpha

    @contextmanager
    def clip(self,points):
        self.clip_id+=1; cid=f'clip{self.clip_id}'
        ps=' '.join(f'{x:.3f},{y:.3f}' for x,y in points)
        self.svg.append(f'<defs><clipPath id="{cid}"><polygon points="{ps}"/></clipPath></defs><g clip-path="url(#{cid})">')
        PDF.saveState(); p=PDF.beginPath(); p.moveTo(points[0][0],H-points[0][1])
        for x,y in points[1:]: p.lineTo(x,H-y)
        p.close(); PDF.clipPath(p,stroke=0,fill=0)
        yield
        PDF.restoreState(); self.svg.append('</g>')

    def rect(self,x,y,w,h,fill,stroke=None,sw=1):
        self.poly([(x,y),(x+w,y),(x+w,y+h),(x,y+h)],fill,stroke,sw)

    def poly(self,points,fill=None,stroke=None,sw=1):
        if fill: PDF.setFillColor(HexColor(fill))
        if stroke: PDF.setStrokeColor(HexColor(stroke)); PDF.setLineWidth(sw)
        PDF.setFillAlpha(self.opacity);PDF.setStrokeAlpha(self.opacity)
        p=PDF.beginPath(); p.moveTo(points[0][0],H-points[0][1])
        for x,y in points[1:]: p.lineTo(x,H-y)
        p.close(); PDF.drawPath(p,fill=bool(fill),stroke=bool(stroke))
        ps=' '.join(f'{x:.3f},{y:.3f}' for x,y in points)
        self.svg.append(f'<polygon points="{ps}" fill="{fill or "none"}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')
        self.elements+=1

    def line(self,points,color,sw=2):
        PDF.setStrokeColor(HexColor(color)); PDF.setLineWidth(sw); PDF.setLineCap(1); PDF.setLineJoin(1)
        PDF.setStrokeAlpha(self.opacity)
        p=PDF.beginPath(); p.moveTo(points[0][0],H-points[0][1])
        for x,y in points[1:]: p.lineTo(x,H-y)
        PDF.drawPath(p,fill=0,stroke=1)
        ps=' '.join(f'{x:.3f},{y:.3f}' for x,y in points)
        self.svg.append(f'<polyline points="{ps}" fill="none" stroke="{color}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"/>')
        self.elements+=1

    def ellipse(self,cx,cy,rx,ry,color,stroke=None,sw=1):
        PDF.setFillColor(HexColor(color))
        if stroke: PDF.setStrokeColor(HexColor(stroke)); PDF.setLineWidth(sw)
        PDF.setFillAlpha(self.opacity);PDF.setStrokeAlpha(self.opacity)
        PDF.ellipse(cx-rx,H-cy-ry,cx+rx,H-cy+ry,fill=1,stroke=bool(stroke))
        self.svg.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{color}" stroke="{stroke or "none"}" stroke-width="{sw}"/>')
        self.elements+=1

    def text(self,x,y,text,size,color=None,bold=False,max_width=None):
        font='ArtBold' if bold else 'ArtRegular'
        tw=pdfmetrics.stringWidth(text,font,size)
        if max_width is not None and tw>max_width: raise ValueError((self.name,'text width',text,tw,max_width))
        if x+tw>W-80 or y-size<65 or y>H-65: raise ValueError((self.name,'text bounds',text))
        col=color or self.fg
        PDF.setFillColor(HexColor(col));PDF.setFillAlpha(self.opacity); PDF.setFont(font,size); PDF.drawString(x,H-y,text)
        self.svg.append(f'<text x="{x}" y="{y}" font-family="Microsoft YaHei, Noto Sans CJK SC, sans-serif" font-size="{size}" font-weight="{700 if bold else 400}" fill="{col}">{html.escape(text)}</text>')
        self.texts.append({'text':text,'x':x,'baseline':y,'size':size,'width':round(tw,2)})

    def finish(self):
        self.svg.append('</svg>'); (SVG/(self.name+'.svg')).write_text('\n'.join(self.svg),encoding='utf-8')
        PDF.restoreState(); PDF.showPage()
        return {'name':self.name,'textElements':self.texts,'vectorElements':self.elements,
                'textBoundsChecked':True,'visualReview':'PENDING'}

def soft_shadow(a,cx,cy,rx,ry):
    for k in range(16,0,-1):
        with a.alpha(.006 if a.dark else .008):
            a.ellipse(cx,cy,rx+k*max(2,rx*.007),ry+k*max(1,ry*.008),'#020C16')

def backdrop(a,index):
    # Quiet large-scale contour signature: visual atmosphere, never fake UI.
    color='#35516B' if a.dark else '#CFD8D1'
    with a.alpha(.20 if a.dark else .32):
        for k in range(12):
            pts=[]
            for j in range(95):
                t=j/94*math.pi*2
                r=580+k*38
                x=1870+math.cos(t)*r*(1+.06*math.sin(t*5+index))
                y=1970+math.sin(t)*r*.66*(1+.09*math.cos(t*3))
                pts.append((x,y))
            a.line(pts,color,1.8)

def frame(a,title_lines,copy):
    a.text(160,215,copy['brand'],64,bold=True,max_width=1510)
    a.text(1730,207,copy['status'],50,a.accent,max_width=510)
    a.line([(160,290),(2240,290)],a.stroke,2)
    a.text(160,506,title_lines[0],174,bold=True,max_width=2050)
    a.text(160,718,title_lines[1],174,a.accent,True,max_width=2050)
    a.text(169,849,copy['subtitle'],60,color=a.fg,max_width=2040)
    a.line([(160,2714),(2240,2714)],a.stroke,2)
    a.text(160,2830,'原创地理概念插画',39,a.accent,max_width=1200)
    a.text(1890,2830,f'{a.index+1:02} / 08',39,a.fg,max_width=340)
    a.text(160,2920,copy['footer'],50,color=a.fg,max_width=2080)

class Iso:
    def __init__(self,ox,oy,sx=49,sy=24,sz=130):
        self.ox,self.oy,self.sx,self.sy,self.sz=ox,oy,sx,sy,sz
    def p(self,x,y,z=0): return self.ox+(x-y)*self.sx,self.oy+(x+y)*self.sy-z*self.sz

def height(x,y,mode=0):
    ridge=2.7*math.exp(-((x+5.4)**2/21+(y-3.6)**2/14))
    ridge+=1.9*math.exp(-((x+6.5)**2/12+(y+4.3)**2/10))
    ridge+=.8*math.exp(-((x-7)**2/18+(y-7)**2/12))
    river=.30+1.35*math.sin(y*.31)
    z=.09+ridge
    if abs(x-river)<1.20: z=.02
    return z

def tile_color(x,y,z,scheme):
    river=.30+1.35*math.sin(y*.31)
    if abs(x-river)<1.20: return scheme['water']
    return mix(scheme['land'],scheme['peak'],min(1,z/3.0))

def cube(a,iso,x,y,w,d,z,h,color,windows=False):
    p=iso.p
    a.poly([p(x,y,z+h),p(x+w,y,z+h),p(x+w,y+d,z+h),p(x,y+d,z+h)],mix(color,'#FFFFFF',.20))
    a.poly([p(x+w,y,z),p(x+w,y+d,z),p(x+w,y+d,z+h),p(x+w,y,z+h)],darken(color,.30))
    a.poly([p(x,y+d,z),p(x+w,y+d,z),p(x+w,y+d,z+h),p(x,y+d,z+h)],darken(color,.12))
    a.line([p(x,y+d,z+h),p(x+w,y+d,z+h),p(x+w,y,z+h)],mix(color,'#FFFFFF',.45),1.3)
    if windows and h>.75:
        for f in range(1, max(2,int(h/.30))):
            zz=z+.21+f*.25
            if zz>z+h-.13: break
            a.line([p(x+.10,y+d+.006,zz),p(x+w-.10,y+d+.006,zz)],mix(color,'#182B39',.48),1.35)
        if w>.7:
            a.line([p(x+w*.48,y+d+.008,z+.08),p(x+w*.48,y+d+.008,z+h-.08)],mix(color,'#182B39',.38),1)

def terrain(a,iso,scheme=None,city=True,trees=False,extra_layer=False,detail=26):
    if scheme is None: scheme={'land':'#CFDDC6','peak':'#718E82','water':'#76B5C4','city':'#F6EFE0'}
    p=iso.p; lo,hi=-10,10; step=20/detail
    # A physical terrain block edge gives depth; top facets carry believable shading.
    for edge in [0,1]:
        for j in range(detail):
            t0=lo+j*step;t1=t0+step
            x0,y0=(hi,t0) if edge==0 else (t0,hi)
            x1,y1=(hi,t1) if edge==0 else (t1,hi)
            c=mix(scheme['land'],'#384B5A',.42 if edge==0 else .23)
            a.poly([p(x0,y0,-.70),p(x1,y1,-.70),p(x1,y1,height(x1,y1)),p(x0,y0,height(x0,y0))],c)
    cells=[(lo+i*step,lo+j*step) for i in range(detail) for j in range(detail)]
    for x,y in sorted(cells,key=lambda q:q[0]+q[1]):
        coords=[(x,y),(x+step,y),(x+step,y+step),(x,y+step)]
        zs=[height(xx,yy) for xx,yy in coords]
        base=tile_color(x+step*.5,y+step*.5,sum(zs)/4,scheme)
        gx=(zs[1]-zs[0])/step;gy=(zs[3]-zs[0])/step
        shade=max(-.19,min(.18, .07*gx-.12*gy))
        col=mix(base,'#FFFFFF',shade) if shade>=0 else darken(base,-shade)
        v=[p(xx,yy,zz) for (xx,yy),zz in zip(coords,zs)]
        a.poly(v,col)
    # Closed contour rings over the mountain slope, genuine parametric geometry.
    for k in range(11):
        rr=.65+k*.49;pts=[]
        for j in range(80):
            t=j/79*math.pi*2
            xx=-5.4+rr*math.cos(t);yy=3.6+rr*.69*math.sin(t)
            if -10<xx<10 and -10<yy<10:pts.append(p(xx,yy,height(xx,yy)+.014))
        if len(pts)>2:
            with a.alpha(.40):a.line(pts,mix(scheme['peak'],'#F0F1DF',.6),1.1)
    for xx in [2.45,3.65,4.85,6.05,7.25,8.45]:
        pts=[p(xx,yy,height(xx,yy)+.016) for yy in [(-8.5+j*.22) for j in range(74)]]
        a.line(pts,'#E9EAD8',4.5)
        a.line(pts,'#869F9A',.85)
    for yy in [-7.0,-5.65,-4.3,-2.95,-1.6,-.25,1.1,2.45,3.8,5.15,6.5]:
        pts=[p(xx,yy,height(xx,yy)+.019) for xx in [(2+j*.12) for j in range(57)]]
        a.line(pts,'#F1EBDD',4.3)
    # One river-side transport line, with no false quantification.
    pts=[]
    for j in range(100):
        yy=-9.6+j*19.2/99;xx=1.6+1.35*math.sin(yy*.31)
        pts.append(p(xx,yy,height(xx,yy)+.028))
    a.line(pts,'#E9B783',7)
    a.line(pts,'#F5E7CC',2)
    if city:
        blocks=[];rng=random.Random(920+int(iso.sx))
        for ix in range(5):
            for iy in range(8):
                x=2.65+ix*1.18;y=-6.7+iy*1.34
                h=.22+rng.random()*.37
                if ix in [0,1,2] and iy in [3,4,5]:h+=rng.random()*2.00
                blocks.append((x,y,.56+rng.random()*.22,.72,h))
        for x,y,bw,bd,bh in sorted(blocks,key=lambda q:q[0]+q[1]):
            cube(a,iso,x,y,bw,bd,height(x,y)+.02,bh,scheme['city'],windows=True)
    if trees:
        rng=random.Random(531)
        for xx,yy in sorted([(rng.uniform(-8.6,-2.7),rng.uniform(-8,7.5)) for _ in range(37)],key=lambda q:sum(q)):
            zz=height(xx,yy);cx,cy=p(xx,yy,zz)
            s=12+rng.random()*10
            a.line([(cx,cy),(cx,cy-s*2)],'#807B61',2)
            a.poly([(cx,cy-s*3.8),(cx-s,cy-s*.6),(cx+s,cy-s*.6)],'#315E57')
            a.poly([(cx,cy-s*3.8),(cx,cy-s*.6),(cx+s,cy-s*.6)],'#467B69')

class Plane:
    def __init__(self,x,y,ax,ay,bx,by):self.x,self.y,self.ax,self.ay,self.bx,self.by=x,y,ax,ay,bx,by
    def p(self,u,v):return self.x+u*self.ax+v*self.bx,self.y+u*self.ay+v*self.by
    def box(self,u,v,w,h):return [self.p(u,v),self.p(u+w,v),self.p(u+w,v+h),self.p(u,v+h)]

def paper(a,q,color='#FBFAF2',thickness=10):
    pts=q.box(0,0,1,1)
    soft_shadow(a,sum(x for x,y in pts)/4,max(y for x,y in pts)+13,
                (max(x for x,y in pts)-min(x for x,y in pts))*.44,
                16+(max(y for x,y in pts)-min(y for x,y in pts))*.02)
    a.poly([(x,y+thickness) for x,y in pts],mix(color,'#586572',.24))
    a.poly(pts,color)
    for f in [.009,.014,.020]:
        a.line([q.p(0,1+f),q.p(1,1+f)],mix(color,'#586572',.12),1)

def flatmap(a,q,scheme=0,margin=.055,variant=0):
    palettes=[('#DFE6CF','#779CAA','#83987C','#BC7652'),
              ('#E5E8E0','#6D99B1','#C1BCAB','#BC7956'),
              ('#E7DCCA','#AFCCCD','#AA9F89','#8A4145'),
              ('#D7E8E5','#80BFB9','#9DC0B4','#C88C58')]
    land,water,grid,route=palettes[scheme%4]
    a.poly(q.box(margin,margin,1-2*margin,1-2*margin),land)
    bounds=q.box(margin,margin,1-2*margin,1-2*margin)
    with a.clip(bounds):
        geographic=scheme%4
        # Four different original geographies: mountain valley, urban corridor,
        # coastal peninsula, and agricultural watersheds. Style is not a recolor.
        if geographic in [0,1]:
            for ix in range(11):
                for iy in range(9):
                    u=.08+ix*.079;v=.07+iy*.096
                    if geographic==0 and u<.32 and .15<v<.78:continue
                    fill=mix(land,grid,.15+.18*((ix*3+iy)%5)/4)
                    a.poly(q.box(u+.01,v+.014,.054,.063),fill)
            for u in [.071+i*.079 for i in range(12)]:
                if geographic==0 and u<.32:continue
                a.line([q.p(u,-.02),q.p(u,1.02)],'#FBFAEE',2.6)
            for v in [.064+i*.096 for i in range(10)]:
                a.line([q.p(.32 if geographic==0 else -.02,v),q.p(1.02,v)],'#FBFAEE',2.6)
            center=[(.52+.15*math.sin(v*6.5),v) if geographic==0 else
                    (.22+.62*v+.055*math.sin(v*11),v) for v in [j/89 for j in range(90)]]
            banks=[q.p(u-.037,v) for u,v in center]+[q.p(u+.037,v) for u,v in reversed(center)]
            a.poly(banks,water)
            a.line([q.p(u-.041,v) for u,v in center],mix(water,'#FFFFFF',.6),2)
        elif geographic==2:
            coast=[(.57+.12*math.sin(v*7.0)+.06*math.sin(v*17),v) for v in [j/89 for j in range(90)]]
            a.poly([q.p(u,v) for u,v in coast]+[q.p(1.05,1.05),q.p(1.05,-.05)],water)
            a.line([q.p(u-.014,v) for u,v in coast],mix(water,'#FBFAF0',.6),3)
            for ix in range(7):
                for iy in range(11):
                    u=.08+ix*.052;v=.05+iy*.081
                    if .16<u<.32 and .24<v<.72:continue
                    a.poly(q.box(u,v,.036,.053),mix(land,grid,.28))
            for n in range(3):
                pts=[q.p(.80+.040*n+(.035+n*.005)*math.cos(t),
                         .30+n*.19+(.055+n*.008)*math.sin(t)) for t in [j/49*math.pi*2 for j in range(50)]]
                a.poly(pts,land,grid,.8)
        else:
            for ix in range(8):
                for iy in range(9):
                    u=.035+ix*.116;v=.045+iy*.103
                    a.poly([q.p(u,v+.014),q.p(u+.093,v),q.p(u+.11,v+.070),q.p(u+.022,v+.084)],
                           mix(land,grid,.18+.20*((ix+iy)%4)/3))
            center=[(.40+.075*math.sin(v*10)+.14*v,v) for v in [j/99 for j in range(100)]]
            a.poly([q.p(u-.027,v) for u,v in center]+[q.p(u+.027,v) for u,v in reversed(center)],water)
            for bx in [.14,.75]:
                a.line([q.p(bx+(.50-bx)*t,.26+t*.34) for t in [j/49 for j in range(50)]],water,8)
        # Hill contour centers and widths also vary with geography.
        for k in range(13):
            r=.020+.014*k
            cx,cy=(.18,.43) if geographic==0 else (.78,.20) if geographic==1 else (.22,.50) if geographic==2 else (.78,.72)
            pts=[q.p(cx+r*math.cos(t)*(1+.10*math.sin(t*4+geographic)),
                     cy+r*(1.40 if geographic==2 else .95)*math.sin(t)) for t in [j/99*math.pi*2 for j in range(100)]]
            a.line(pts,mix(grid,'#F8F1DC',.20),1.2)
        paths=[[(.12,.78),(.28,.68),(.44,.72),(.61,.54),(.78,.52),(.90,.24)],
               [(.09,.23),(.27,.35),(.42,.28),(.59,.48),(.72,.68),(.91,.76)],
               [(.10,.83),(.15,.60),(.31,.38),(.35,.17),(.46,.11)],
               [(.11,.38),(.30,.46),(.39,.68),(.63,.78),(.81,.84)]]
        path=paths[geographic]
        a.line([q.p(u,v) for u,v in path],route,5)
        for u,v in path[1:-1]:
            x,y=q.p(u,v);a.ellipse(x,y,7,7,'#F8F5EA',route,2)
        if variant:
            a.poly([q.p(.73,.57),q.p(.86,.57),q.p(.87,.73),q.p(.75,.73)],mix(route,land,.42),route,1.2)
    # Printed edge detail, no fake scale, legend numbers, claims or software UI.
    a.line([q.p(margin,.965),q.p(.42,.965)],grid,2)
    a.line([q.p(.65,.965),q.p(1-margin,.965)],route,4)

def map_sheet(a,q,scheme=0,variant=0):
    paper(a,q);flatmap(a,q,scheme,variant=variant)

def book(a,x,y,scale=1,scheme=0):
    # Two pages meet at a single, physically coherent spine.
    left=Plane(x-690*scale,y-70*scale,690*scale,70*scale,-100*scale,500*scale)
    right=Plane(x,y,690*scale,-180*scale,85*scale,480*scale)
    paper(a,left,'#FAF8EF',17);paper(a,right,'#F6F5EA',17)
    flatmap(a,left,scheme,margin=.07)
    flatmap(a,right,(scheme+1)%4,margin=.07,variant=1)
    a.line([(x,y),(x-14*scale,y+489*scale)],'#A7A694',3)

def page01(a):
    soft_shadow(a,1190,2310,920,205)
    terrain(a,Iso(1220,1770,48,24,156),city=True,trees=True)
    map_sheet(a,Plane(418,2230,850,-120,75,340),1)
    book(a,1630,2290,.61,0)

def page02(a):
    # An unfolded map ribbon anchors the city model and a single intent sheet.
    map_sheet(a,Plane(276,1370,1450,440,-125,650),3)
    a.poly([(1726,1810),(1954,1770),(2060,2100),(1601,2460),(1562,2160)],'#E8DCC3')
    for i in range(8):
        a.line([(1820+i*12,1810),(2010+i*4,2088),(1680+i*3,2395)],'#D8C9AD',1)
    terrain(a,Iso(1410,1890,33,16.5,113),city=True,trees=False,detail=24)
    q=Plane(259,2090,740,110,-55,350)
    paper(a,q,'#FFFDF6',9)
    for i,l in enumerate([.73,.58,.76,.50]):
        a.line([q.p(.11,.22+i*.15),q.p(l,.22+i*.15)],'#9AADA5',3)
    a.line([q.p(.11,.86),q.p(.36,.86)],CORAL,5)

def page03(a):
    # A full-scale print composition in a dramatically different camera view.
    q=Plane(355,1160,1580,-140,50,1280)
    paper(a,q,'#FBF8ED',18)
    area=Plane(q.p(.055,.16)[0],q.p(.055,.16)[1],q.ax*.89,q.ay*.89,q.bx*.68,q.by*.68)
    flatmap(a,area,0,margin=.015)
    a.line([q.p(.06,.084),q.p(.54,.084)],'#294D4B',16)
    a.line([q.p(.06,.115),q.p(.27,.115)],'#BC7956',5)
    for i,l in enumerate([.30,.45,.32]):
        a.line([q.p(.06,.90+i*.023),q.p(l,.90+i*.023)],'#ABB7A7',2.4)
    a.line([q.p(.76,.94),q.p(.92,.94)],TEAL,6)
    for i,col in enumerate(['#527C73','#7BA6A8','#E1B588','#B97051']):
        qq=Plane(1660+i*66,2440+i*10,135,10,-10,87)
        paper(a,qq,'#FBFAF2',4);a.poly(qq.box(.12,.15,.75,.65),col)

def page04(a):
    # Rotated sheets deliberately vary palette and angle without flowchart frames.
    map_sheet(a,Plane(232,1400,940,-170,140,1120),2)
    map_sheet(a,Plane(1150,1240,930,130,-120,1110),3)
    book(a,1260,2190,.88,1)
    for i,col in enumerate(['#9AB6A3','#80A6B4','#DABB92','#95544C']):
        q=Plane(242+i*75,2570+i*4,150,12,-10,75)
        paper(a,q,'#FAF7EF',3);a.poly(q.box(.1,.15,.8,.65),col)

def page05(a):
    # Exploded geographic layers. Thin sheet geometry, no fake control console.
    iso=Iso(1230,2180,43,21,103)
    soft_shadow(a,1200,2530,860,140)
    terrain(a,iso,{'land':'#B7C4B8','peak':'#697F78','water':'#7799AC','city':'#E7E5D9'},city=False,trees=True)
    for z,col,kind in [(1.60,'#B5CBCD',0),(3.40,'#98C5C9',1),(5.20,'#DDAE8C',2)]:
        p=lambda x,y:iso.p(x,y,z)
        outline=[p(-10,-10),p(10,-10),p(10,10),p(-10,10)]
        with a.alpha(.17 if kind<2 else .24):a.poly(outline,col,'#A9C9C5',1.3)
        a.line([p(-10,10),p(10,10),p(10,-10)],'#E4B588' if kind==2 else '#8DCEC5',1.9)
        if kind==0:
            for xx in [-7,-4,-1,2,5,8]:a.line([p(xx,-9),p(xx,9)],'#507C82',1.6)
            for yy in [-7,-4,-1,2,5,8]:a.line([p(-9,yy),p(9,yy)],'#507C82',1.6)
        elif kind==1:
            points=[p(.4+1.5*math.sin(y*.31),y) for y in [j*.20-9 for j in range(91)]]
            a.line(points,'#70B7C7',28);a.line(points,'#B4E0DF',5)
        else:
            for i in range(11):
                r=1+i*.48
                a.line([p(-4.0+r*math.cos(t),2+r*.7*math.sin(t)) for t in [j/79*math.pi*2 for j in range(80)]],
                       '#D5A371',1.7)
    # Registration lines look like precise architectural assembly, not a network.
    for xx,yy in [(-10,-10),(10,-10),(10,10),(-10,10)]:
        with a.alpha(.36):a.line([iso.p(xx,yy,-.6),iso.p(xx,yy,5.2)],'#ADC9C5',1.4)

def page06(a):
    # One updated map and a full report occupy different orientations and sizes.
    # Their shared coral motif communicates dependency without repeated maps.
    map_sheet(a,Plane(170,1300,1030,-100,20,1100),1,variant=1)
    q=Plane(1330,1310,835,150,-55,1120)
    paper(a,q,'#FAF8EF',15)
    a.line([q.p(.10,.095),q.p(.69,.095)],INK,13)
    a.line([q.p(.10,.13),q.p(.36,.13)],CORAL,4)
    for i in range(4):a.line([q.p(.1,.20+i*.026),q.p(.87,.20+i*.026)],'#BECBC1',2.7)
    # Chart and rule-table are illustrative document graphics, never real data.
    for i,bh in enumerate([.12,.20,.16,.27]):
        a.poly(q.box(.17+i*.17,.60-bh,.10,bh),CORAL if i==3 else '#86AEA4')
    a.line([q.p(.1,.64),q.p(.9,.64)],'#839A8B',2)
    for r in range(5):a.line([q.p(.10,.72+r*.035),q.p(.89,.72+r*.035)],'#C2CDC2',2)
    for u in [.10,.37,.60,.89]:a.line([q.p(u,.72),q.p(u,.86)],'#C2CDC2',2)
    # A smaller landscape map sheet and source page suggest the synchronized set.
    map_sheet(a,Plane(440,2300,830,-80,45,290),1,variant=1)
    a.line([(1150,1850),(1235,1850),(1273,1858)],CORAL,3)
    a.ellipse(1220,1850,7,7,CORAL)

def page07(a):
    # A fold-out geographical atlas replaces the cover's single terrain block.
    # Three connected physical pages tell planning, research, and database work.
    q1=Plane(180,1590,680,-270,80,750)
    q2=Plane(860,1320,675,290,80,750)
    q3=Plane(1535,1610,675,-145,80,750)
    for q,s in [(q1,1),(q2,3),(q3,2)]:paper(a,q,'#F1EEDB',13);flatmap(a,q,s,margin=.045)
    a.line([q1.p(1,0),q1.p(1,1)],'#D7CEAE',3)
    a.line([q2.p(1,0),q2.p(1,1)],'#D7CEAE',3)
    # A compact facility district rises from the first atlas panel.
    iso=Iso(550,1810,20,10,67)
    for x,y,w,d,h in [(-3,-1,1.3,1.2,2.7),(-1,-2,1.1,1.2,4.6),
                      (1,-1,1.3,1.5,3.4),(-1,1,1.3,1.2,1.9),(2,2,1.1,1.1,2.2)]:
        cube(a,iso,x,y,w,d,0,h,'#F4E6D3',True)
    # Research map has measured-looking but nonnumeric contour relief.
    for k in range(10):
        r=.025+.019*k
        pts=[q2.p(.48+r*math.cos(t),.48+r*1.1*math.sin(t)) for t in [j/79*math.pi*2 for j in range(80)]]
        a.line(pts,'#668D76',1.6)
    # The database panel carries a discreet indexed sheet, with no fake numbers.
    a.poly(q3.box(.58,.18,.31,.34),'#F8F4E6')
    for yy in [.24,.30,.36,.42,.48]:a.line([q3.p(.62,yy),q3.p(.85,yy)],'#88A09A',1.8)
    a.line([q3.p(.62,.22),q3.p(.78,.22)],CORAL,3)
    a.text(190,2550,'规划汇报   科研专题   规范建库',48,a.accent,max_width=1980)

def page08(a):
    # A close, open atlas: the outcome is the focal object.
    terrain(a,Iso(1780,1450,24,12,70),city=True,trees=False,detail=20)
    q=Plane(420,1180,830,-70,0,860)
    paper(a,q,'#EAEBE0',14);flatmap(a,q,2)
    for i in range(3):
        qq=Plane(420-i*45,1260+i*105,810,-60,0,880)
        with a.alpha(.63):a.poly(qq.box(0,0,1,1),'#DBE4DC','#A9BBB4',1.8)
        with a.alpha(.63):
            for k in range(8):
                rr=.06+k*.031
                a.line([qq.p(.48+rr*math.cos(t),.43+rr*.9*math.sin(t)) for t in [j/79*math.pi*2 for j in range(80)]],
                       '#88A59B',1.3)
    book(a,1250,2035,1.12,0)

TITLES=[['从分析走向','完整交付'],['用一句话','开启任务'],['让专业成图','自动完成'],
        ['让参考风格','成为灵感'],['让新能力','持续加入'],['让整套成果','同步更新'],
        ['让更多业务','形成成果'],['领取一套','完整的成果']]
SCENES=[page01,page02,page03,page04,page05,page06,page07,page08]
records=[]
for i,(name,scene) in enumerate(zip(NAMES,SCENES)):
    a=Art(name,i,dark=i in [0,2,4,6])
    backdrop(a,i)
    scene(a)
    # Brand and footer are drawn last so artwork never covers factual qualifiers.
    frame(a,TITLES[i],COPY['pages'][i])
    records.append(a.finish())
PDF.save()

doc=pdfium.PdfDocument(str(PDF_PATH))
assert len(doc)==8
for i,page in enumerate(doc):
    image=page.render(scale=W/page.get_width()).to_pil()
    if image.size!=(W,H):image=image.resize((W,H))
    image.save(PNG/(NAMES[i]+'.png'),dpi=(254,254))
    textpage=page.get_textpage();text=textpage.get_text_range()
    assert '3.0 方案预告' in text and '实际验收为准' in text
    assert '\ufffd' not in text
    records[i]['pdfText']=text;records[i]['renderedPixels']=list(image.size)
    textpage.close()
doc.close()
manifest={'status':'AUTHORING_AND_RENDER_COMPLETE_VISUAL_REVIEW_PENDING',
          'method':'Original parameterized isometric/cartographic vector illustration; ReportLab PDF and SVG; PDFium rasterization',
          'imagegenCalled':False,'reason':'User explicitly selected D-drive-only native vector campaign',
          'productAcceptance':'NOT_RUN','pdfPages':8,'pngDimensions':[W,H],
          'designs':records,
          'files':[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,
                    'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()}
                   for folder in [PNG,SVG,BOOK] for p in sorted(folder.iterdir()) if p.is_file()]}
(QA/'矢量图集制作回执.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'png':8,'svg':8,'pdfPages':8,'root':str(ROOT)},ensure_ascii=False))
