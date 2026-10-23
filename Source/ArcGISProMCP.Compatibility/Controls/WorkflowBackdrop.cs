using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ArcGISProMCP.Compatibility.Controls;

/// <summary>Original atmospheric landscape: cached curved ridgelines, mist and quiet water. Never a GIS preview.</summary>
public sealed class WorkflowBackdrop : FrameworkElement
{
    private const double H=1000;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,DrawingGroup> Scenes=new();
    private static readonly Brush DayMist=Mist("#50F6F6E9"),NightMist=Mist("#287FAF9E"),MoonHalo=Mist("#65EAE8C3"),SunHalo=Mist("#80FDF2D5");
    private static readonly Brush Star=B("#A9D4DFCB"),Firefly=B("#ABD8CC");
    private static readonly Pen Ripple=FrozenPen("#7BD8E5D5",1.0),Bird=FrozenPen("#59456351",1.4);
    private string _theme="MOUNTAIN";
    private double _time;
    private string? _previousTheme;
    private double _transitionStart;
    private Size _renderSize;
    private RectangleGeometry? _clip;
    private MatrixTransform? _transform;
    public WorkflowBackdrop() { IsHitTestVisible=false;Focusable=false;ClipToBounds=true; }
    public void SetTheme(string theme,bool animate=false)
    { if(theme==_theme)return;_previousTheme=animate?_theme:null;_transitionStart=_time;_theme=theme;InvalidateVisual(); }
    public void FinishTransition() { if(_previousTheme is null)return;_previousTheme=null;InvalidateVisual(); }
    public void SetFrame(double seconds) { _time=seconds;InvalidateVisual(); }
    internal static (string Theme,string Label) ForLocalTime(DateTime localTime)=>localTime.Hour switch {
        >=5 and <8 => ("DAWN","晨曦云海"),>=8 and <17 => ("MOUNTAIN","日间山海"),
        >=17 and <20 => ("DUSK","暮色映湖"),_ => ("AURORA","夜间星野")
    };
    protected override AutomationPeer OnCreateAutomationPeer()=>new SceneryPeer(this);
    private sealed class SceneryPeer : FrameworkElementAutomationPeer
    {
        public SceneryPeer(WorkflowBackdrop owner):base(owner) { }
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Pane;
        protected override bool IsControlElementCore()=>true;
        protected override bool IsContentElementCore()=>false;
        protected override string GetNameCore()=>"山水装饰 · 非任务地图成果";
        protected override string GetItemStatusCore()=>"scenery-frame:"+((WorkflowBackdrop)Owner)._time.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+";theme:"+((WorkflowBackdrop)Owner)._theme;
    }
    private static Color C(string hex)=>(Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
    private static SolidColorBrush B(string hex) { var b=new SolidColorBrush(C(hex));b.Freeze();return b; }
    private static Brush Mist(string hex) { var b=new RadialGradientBrush(C(hex),Colors.Transparent);b.Freeze();return b; }
    private static Pen FrozenPen(string hex,double width) { var p=new Pen(B(hex),width);p.Freeze();return p; }
    private static LinearGradientBrush Gradient(params string[] colors)
    {
        var b=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(0,1) };
        for(var i=0;i<colors.Length;i++)b.GradientStops.Add(new GradientStop(C(colors[i]),i/(double)(colors.Length-1)));
        b.Freeze();return b;
    }
    // Catmull-Rom control points produce continuous organic contours, without triangular silhouettes.
    private static StreamGeometry Ridge(double width,double baseline,double amplitude,int seed)
    {
        var points=Enumerable.Range(0,15).Select(i=>new Point(-50+(width+100)*i/14,
            baseline+Math.Sin(i*.58+seed*.9)*amplitude+Math.Sin(i*1.31+seed*.67)*amplitude*.28)).ToArray();
        var g=new StreamGeometry();using(var c=g.Open()) {
            c.BeginFigure(points[0],true,true);
            for(var i=0;i<points.Length-1;i++) {
                var a=points[Math.Max(0,i-1)];var b=points[i];var e=points[i+1];var f=points[Math.Min(points.Length-1,i+2)];
                c.BezierTo(new Point(b.X+(e.X-a.X)/6,b.Y+(e.Y-a.Y)/6),new Point(e.X-(f.X-b.X)/6,e.Y-(f.Y-b.Y)/6),e,true,false);
            }
            c.LineTo(new Point(width+50,H+20),true,false);c.LineTo(new Point(-50,H+20),true,false);
        }
        g.Freeze();return g;
    }
    private static StreamGeometry Arc(Point start,Point a,Point b,Point end)
    {
        var g=new StreamGeometry();using(var c=g.Open()) { c.BeginFigure(start,false,false);c.BezierTo(a,b,end,true,false); }g.Freeze();return g;
    }
    private static DrawingGroup Scene(string theme,bool portrait)
    {
        var key=theme+(portrait?"-portrait":"-panorama");if(Scenes.TryGetValue(key,out var cached))return cached;
        var night=theme=="AURORA";var dawn=theme=="DAWN";var dusk=theme=="DUSK";var w=portrait?400d:1600d;
        var group=new DrawingGroup();using(var d=group.Open()) {
            d.DrawRectangle(night?Gradient("#193E3A","#52776A","#A3B29A"):dusk?Gradient("#5C5367","#BD9589","#DDD0AF"):dawn?Gradient("#829D92","#C4C9AD","#E8DCC0"):Gradient("#84AD9B","#BED2B7","#E2DFC2"),null,new Rect(0,0,w,H));
            var sun=new Point(w*(portrait?.62:.70),portrait?285:210);
            d.DrawEllipse(night?MoonHalo:SunHalo,null,sun,portrait?105:180,140);
            d.DrawEllipse(B(night?"#F1EACB":"#FFF1D1"),null,sun,night?26:33,night?26:33);
            if(night) {
                var random=new Random(51);
                for(var i=0;i<(portrait?30:75);i++) { var p=new Point(random.NextDouble()*w,40+random.NextDouble()*380);d.DrawEllipse(Star,null,p,i%7==0?1.4:.7,i%7==0?1.4:.7); }
            }
            var colors=night?new[] {"#A0B3A0","#839F8E","#658778","#466D61","#2C574F"}:dusk?new[] {"#C2B9AC","#A3A898","#818F7E","#657B6C","#3E6158"}:dawn?new[] {"#C6CFB7","#ACBFA7","#8FAE98","#6B9582","#3E7467"}:new[] {"#BED1BA","#99B89D","#7AA489","#548B73","#2E6A5B"};
            for(var i=0;i<colors.Length;i++)d.DrawGeometry(Gradient(colors[i],i==4?"#355E50":colors[Math.Min(i+1,4)]),null,Ridge(w,425+i*73,portrait?56-i*4:70-i*5,i+2));
            // Soft aerial perspective lies between contours; no grid, spinning compass or geometric beam.
            var fog=night?NightMist:DayMist;
            d.DrawEllipse(fog,null,new Point(w*.35,510),w*.7,90);d.DrawEllipse(fog,null,new Point(w*.83,660),w*.65,72);
            d.DrawGeometry(night?Gradient("#739486","#254E46"):dusk?Gradient("#B2AE95","#4D7865"):Gradient("#B4CEB0","#4F927B"),null,Ridge(w,795,12,12));
            // Tiny ink-like trees on a distant bank give scale without a hard triangular tree icon.
            var islandX=w*.73;d.DrawEllipse(B(night?"#435F50":"#739076"),null,new Point(islandX,795),portrait?32:63,3);
            for(var i=0;i<3;i++) {
                var x=islandX+(i-1)*13;var height=i==1?35:24;var trunk=FrozenPen(night?"#425E4C":"#5B795F",1.2);
                d.DrawLine(trunk,new Point(x,795),new Point(x,795-height));
                for(var branch=0;branch<4;branch++) {
                    var y=795-height+6+branch*6;var reach=3+branch*2;
                    d.DrawGeometry(null,trunk,Arc(new Point(x-reach,y+3),new Point(x-3,y+1),new Point(x,y-3),new Point(x,y-4)));
                    d.DrawGeometry(null,trunk,Arc(new Point(x,y-4),new Point(x+1,y-1),new Point(x+4,y+1),new Point(x+reach,y+3)));
                }
            }
            d.DrawGeometry(Gradient(night?"#2E574B":"#477B61",night?"#183D35":"#285B48"),null,Ridge(w,920,43,17));
            if(!night)for(var i=0;i<3;i++) {
                var x=w*(portrait?.42:.62)+i*16;var y=360+i*12;
                d.DrawGeometry(null,Bird,Arc(new Point(x-8,y+2),new Point(x-4,y-4),new Point(x-1,y-2),new Point(x,y+1)));
                d.DrawGeometry(null,Bird,Arc(new Point(x,y+1),new Point(x+2,y-3),new Point(x+6,y-2),new Point(x+9,y+1)));
            }
            // A few curved reed leaves anchor the near bank, drawn once per scene.
            var reed=FrozenPen(night?"#71304D3F":"#63406346",1.4);
            for(var i=0;i<7;i++) {
                var x=w*.24+i*7;d.DrawGeometry(null,reed,Arc(new Point(x,1000),new Point(x-6,955),new Point(x+8,935-i*5),new Point(x-12,918-i*3)));
            }
        }
        group.Freeze();Scenes[key]=group;return group;
    }
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);if(ActualWidth<=0||ActualHeight<=0)return;
        var portrait=ActualWidth/ActualHeight<.8;var w=portrait?400d:1600d;var scale=Math.Max(ActualWidth/w,ActualHeight/H);
        if(_clip is null||_renderSize.Width!=ActualWidth||_renderSize.Height!=ActualHeight) {
            _renderSize=new Size(ActualWidth,ActualHeight);_clip=new RectangleGeometry(new Rect(_renderSize));_clip.Freeze();
            _transform=new MatrixTransform(scale,0,0,scale,(ActualWidth-w*scale)/2,(ActualHeight-H*scale)/2);_transform.Freeze();
        }
        d.PushClip(_clip);d.PushTransform(_transform!);
        var blend=Math.Clamp((_time-_transitionStart)/1.2,0,1);var blending=_previousTheme is not null&&blend<1;
        if(blending) { d.DrawDrawing(Scene(_previousTheme!,portrait));d.PushOpacity(blend*blend*(3-2*blend)); }else _previousTheme=null;
        d.DrawDrawing(Scene(_theme,portrait));var night=_theme=="AURORA";
        for(var i=0;i<3;i++) {
            var x=w*(.18+i*.42)+Math.Sin(_time/22+i)*w*.12;var y=520+i*115+Math.Sin(_time/18+i)*9;
            d.DrawEllipse(night?NightMist:DayMist,null,new Point(x,y),w*.62,36+i*6);
        }
        for(var i=0;i<10;i++) {
            var x=w*(.18+i*.068)+Math.Sin(_time/6+i)*6;var y=814+i*7+Math.Cos(_time/8+i)*2;
            d.PushOpacity(.18+.1*Math.Sin(_time/5+i));d.DrawLine(Ripple,new Point(x-5-i%4*3,y),new Point(x+5+i%4*3,y));d.Pop();
        }
        if(night)for(var i=0;i<4;i++) {
            d.PushOpacity(.20+.08*Math.Sin(_time/9+i));d.DrawEllipse(Firefly,null,new Point(w*(.23+i*.16)+Math.Sin(_time/14+i)*9,760+i*24+Math.Cos(_time/11+i)*6),1.5,1.5);d.Pop();
        }
        if(blending)d.Pop();d.Pop();d.Pop();
    }
}
