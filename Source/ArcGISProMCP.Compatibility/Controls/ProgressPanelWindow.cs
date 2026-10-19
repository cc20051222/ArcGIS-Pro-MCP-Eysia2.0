using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Progress;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using ProgressBar = System.Windows.Controls.ProgressBar;
using ListBox = System.Windows.Controls.ListBox;
using TabControl = System.Windows.Controls.TabControl;
using Brushes = System.Windows.Media.Brushes;
using Control = System.Windows.Controls.Control;
namespace ArcGISProMCP.Compatibility.Controls;

/// <summary>Read-only business workflow view shared by Pro and standalone monitor.</summary>
public sealed class ProgressPanelWindow : Window
{
    private readonly Func<IReadOnlyList<ToolProgressEvent>> _read;
    private readonly Func<string?> _journalError;
    private readonly string? _taskRecord;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _goal,_context,_overview,_freshness,_internal,_active,_intakeSummary;
    private readonly ListBox _steps;
    private readonly StackPanel _details,_quality,_events,_intake;
    private JsonElement? _view;
    private JsonElement[] _rows=System.Array.Empty<JsonElement>();
    private IReadOnlyList<ToolProgressEvent> _observed=System.Array.Empty<ToolProgressEvent>();
    private bool _paused;
    private bool _follow=true;
    private readonly Button _followButton;
    private string _recordFingerprint="",_eventFingerprint="";
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(225,234,246));
    private static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(155,173,196));
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(68,214,185));
    private static readonly Brush Warn=new SolidColorBrush(Color.FromRgb(255,191,102));
    public ProgressPanelWindow(Func<IReadOnlyList<ToolProgressEvent>> read,Func<string?>? journalError=null,
        string? taskRecord=null,string sourceLabel="当前 Pro 进程")
    {
        _read=read;_journalError=journalError??(()=>null);_taskRecord=taskRecord;
        Title="ArcGIS Pro MCP · 任务与质量面板";Width=1280;Height=850;MinWidth=1000;MinHeight=650;
        Background=new SolidColorBrush(Color.FromRgb(12,20,34));Foreground=Ink;
        FontFamily=new FontFamily("Microsoft YaHei UI");WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new Grid { Margin=new Thickness(24) };Content=root;
        root.RowDefinitions.Add(new() { Height=GridLength.Auto });root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        root.RowDefinitions.Add(new());root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        var header=new DockPanel();
        _followButton=new Button { Content="跟随当前步骤：开",Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,10,0),Foreground=Ink,Background=new SolidColorBrush(Color.FromRgb(33,49,71)),BorderThickness=new Thickness(0) };
        _followButton.Click+=(_,_)=> { _follow=!_follow;_followButton.Content=_follow?"跟随当前步骤：开":"跟随当前步骤：关";RenderSteps();RenderDetails(); };
        DockPanel.SetDock(_followButton,Dock.Right);header.Children.Add(_followButton);
        var pause=new Button { Content="暂停面板刷新",Padding=new Thickness(14,8,14,8),Foreground=Ink,
            Background=new SolidColorBrush(Color.FromRgb(33,49,71)),BorderThickness=new Thickness(0) };
        pause.Click+=(_,_)=> { _paused=!_paused;pause.Content=_paused?"恢复面板刷新":"暂停面板刷新";Refresh(); };
        DockPanel.SetDock(pause,Dock.Right);header.Children.Add(pause);header.Children.Add(Text("完整处理流程 · 检查 · 修复闭环",24,Ink));root.Children.Add(header);
        var summary=new StackPanel { Margin=new Thickness(0,12,0,14) };
        _goal=Text("等待 Agent 发布本次完整业务流程",20,Ink);_context=Text("先读数据与需求，再制定方法和检查判据；计划不等于执行。",12,Muted);
        _overview=Text("完整处理步骤在主视图；内部日志单独查看。",13,Accent);_freshness=Text(sourceLabel,11,Muted);
        _active=Text("当前无已观测的执行中步骤",15,Accent);_intakeSummary=Text("资料与成果位置尚未记录",12,Warn);
        foreach(var x in new[] {_goal,_context,_active,_intakeSummary,_overview,_freshness})summary.Children.Add(x);
        Grid.SetRow(summary,1);root.Children.Add(summary);
        var body=new Grid();body.ColumnDefinitions.Add(new() { Width=new GridLength(.62,GridUnitType.Star) });body.ColumnDefinitions.Add(new() { Width=new GridLength(.38,GridUnitType.Star) });
        var left=new Grid { Margin=new Thickness(0,0,18,0) };left.RowDefinitions.Add(new() { Height=GridLength.Auto });left.RowDefinitions.Add(new());
        left.Children.Add(Text("全流程总览 · 点击查看方法、输入输出、检查依据与结果",14,Accent));
        _steps=new ListBox { Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Ink,HorizontalContentAlignment=System.Windows.HorizontalAlignment.Stretch,Margin=new Thickness(0,10,0,0) };
        var itemTemplate=new ControlTemplate(typeof(ListBoxItem));
        var border=new FrameworkElementFactory(typeof(Border));border.Name="StepCard";
        border.SetValue(Border.BackgroundProperty,Brushes.Transparent);border.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
        border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));
        border.AppendChild(presenter);itemTemplate.VisualTree=border;
        var selected=new Trigger { Property=ListBoxItem.IsSelectedProperty,Value=true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(24,45,64)),"StepCard"));
        selected.Setters.Add(new Setter(Border.BorderBrushProperty,Accent,"StepCard"));itemTemplate.Triggers.Add(selected);
        _steps.ItemContainerStyle=new Style(typeof(ListBoxItem)) { Setters={new Setter(Control.TemplateProperty,itemTemplate)} };
        ScrollViewer.SetHorizontalScrollBarVisibility(_steps,ScrollBarVisibility.Disabled);
        _steps.SelectionChanged+=(_,_)=>RenderDetails();Grid.SetRow(_steps,1);left.Children.Add(_steps);body.Children.Add(left);
        _steps.PreviewMouseDown+=(_,_)=> { _follow=false;_followButton.Content="跟随当前步骤：关"; };
        var tabs=new TabControl { Background=new SolidColorBrush(Color.FromRgb(18,29,45)),BorderThickness=new Thickness(0) };
        _details=new();_quality=new();_events=new();_intake=new();
        tabs.Items.Add(new TabItem { Header="步骤详情",Content=Scroll(_details) });tabs.Items.Add(new TabItem { Header="检查与修复",Content=Scroll(_quality) });
        var internals=new StackPanel();_internal=Text("等待内部事件",12,Muted);internals.Children.Add(_internal);internals.Children.Add(_events);
        tabs.Items.Add(new TabItem { Header="内部进度",Content=Scroll(internals) });Grid.SetColumn(tabs,1);body.Children.Add(tabs);Grid.SetRow(body,2);root.Children.Add(body);
        tabs.Items.Add(new TabItem { Header="资料与交付",Content=Scroll(_intake) });
        var footer=Text("500 ms读取更新 · Agent发布方案与真实检查记录；SDK事件关联具体步骤 · 关闭仅停止观看，不取消GIS，不自动修复或重试。",11,Muted);
        footer.Margin=new Thickness(0,14,0,0);Grid.SetRow(footer,3);root.Children.Add(footer);
        _timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(MCPSettings.ProgressPanelRefreshMilliseconds) };
        _timer.Tick+=(_,_)=>Refresh();Loaded+=(_,_)=> { Refresh();_timer.Start(); };Closed+=(_,_)=>_timer.Stop();
    }
    private static TextBlock Text(string v,double size,Brush color)=>new() { Text=v,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4) };
    private static ScrollViewer Scroll(UIElement content)=>new() { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(10) };
    private static string Get(JsonElement e,string k,string fallback="")=>e.TryGetProperty(k,out var p)&&p.ValueKind!=JsonValueKind.Null?p.ValueKind==JsonValueKind.String?p.GetString()??fallback:p.ToString():fallback;
    private static JsonElement[] Items(JsonElement e,string k)=>e.TryGetProperty(k,out var p)&&p.ValueKind==JsonValueKind.Array?p.EnumerateArray().ToArray():System.Array.Empty<JsonElement>();
    private static string Join(JsonElement e,string k)=>string.Join("；",Items(e,k).Select(x=>x.ToString()));
    private static string State(string s)=>s switch {
        "QUEUED"=>"等待准入","RUNNING"=>"执行中","PRODUCED"=>"已产出 · 待检查","VERIFIED"=>"已核验","FAILED" or "FAIL"=>"检查失败",
        "BLOCKED"=>"受阻","WAITING_USER"=>"等待用户","UNKNOWN"=>"待核查","PLANNED" or "NOT_RUN"=>"待执行","STALE"=>"已失效 · 需复检",
        "PASS"=>"检查通过","OPEN"=>"问题未解决","RESOLVED"=>"已修复并复检","REPAIRING"=>"修复中","RECHECKING"=>"复检中",
        "NOT_CONFIGURED"=>"未配置","NOT_READY"=>"尚未形成交付候选","PASS_CANDIDATE"=>"技术候选 · 等待验收",
        "NOT_REQUESTED"=>"未申请验收","WAITING_REVIEW"=>"等待独立验收","ACCEPTED"=>"记录已有外部验收","REJECTED"=>"验收拒收",
        "NOT_APPLICABLE"=>"不适用","CANCELLED"=>"已确认取消","CANCEL_REQUESTED"=>"已请求取消，待确认","SKIPPED"=>"已跳过",_=>s };
    private static string Kind(string s)=>s switch { "PREFLIGHT"=>"预检","CHECK"=>"首检","REPAIR"=>"修复","RECHECK"=>"复检","FINAL"=>"最终检查","ACCEPTANCE"=>"验收",_=>"处理" };
    private static string Phase(JsonElement check)=>Get(check,"phase")=="PROCESS"?"过程首检":Kind(Get(check,"phase"));
    private static string Coverage(JsonElement check)
    {
        if(!check.TryGetProperty("scope",out var scope)||scope.ValueKind!=JsonValueKind.Object)return "尚未记录检查覆盖";
        return (Get(scope,"mode")=="FULL"?"全量":"抽样")+"：已检查 "+Get(scope,"checked","未知")+" / 总体 "+Get(scope,"population","未知");
    }
    private static Brush Tint(string s)=>s is "VERIFIED" or "PASS" or "RESOLVED"?Accent:s is "FAILED" or "FAIL" or "BLOCKED" or "STALE" or "OPEN"?Warn:Ink;
    private string Effective(JsonElement r)=>WorkflowEventBinding.EffectiveState(r,WorkflowEventBinding.Latest(r,_observed));
    private bool NewExecution()=>_rows.Any(r=>WorkflowEventBinding.ChecksNeedRefresh(r,WorkflowEventBinding.Latest(r,_observed)));
    private void Refresh()
    {
        if(_paused) { _freshness.Text="面板已暂停；后端执行不受影响";return; }
        try {
            _observed=_read();var fp=string.IsNullOrWhiteSpace(_taskRecord)||!File.Exists(_taskRecord)?"missing":File.GetLastWriteTimeUtc(_taskRecord).Ticks+":"+new FileInfo(_taskRecord).Length;
            var ef=_observed.LastOrDefault()?.Sequence.ToString()??"none";
            if(fp==_recordFingerprint&&ef==_eventFingerprint) { Freshness();return; }
            if(fp!=_recordFingerprint)LoadRecord(fp);_eventFingerprint=ef;
            RenderSteps();RenderDetails();RenderQuality();RenderIntake();RenderEvents();Freshness();
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) {
            _view=null;_rows=System.Array.Empty<JsonElement>();_steps.Items.Clear();_details.Children.Clear();_quality.Children.Clear();_intake.Children.Clear();
            _active.Text="当前步骤未知：记录不可用";_intakeSummary.Text="资料和成果位置待核对";
            _goal.Text="任务记录暂不可用；此前状态不能作为当前进度";_overview.Text=ex.GetType().Name+"；等待有效记录，不重发GIS";_freshness.Text="读取失败";_recordFingerprint="";
        }
    }
    private void LoadRecord(string fp)
    {
        _view=null;_rows=System.Array.Empty<JsonElement>();
        if(fp!="missing") {
            if(new FileInfo(_taskRecord!).Length>MCPSettings.WorkflowRecordMaxBytes)throw new InvalidOperationException("Oversized task record");
            using var d=JsonDocument.Parse(File.ReadAllText(_taskRecord!));
            if(d.RootElement.TryGetProperty("workflowView",out var v)) {
                if(Get(v,"version")!="1")throw new InvalidOperationException("Unknown workflow view");
                _view=v.Clone();_rows=Items(_view.Value,"steps");
                if(_rows.Length is <1 or >300||_rows.Select(r=>Get(r,"id")).Distinct().Count()!=_rows.Length)throw new InvalidOperationException("Invalid workflow steps");
            }
        }
        _recordFingerprint=fp;
        if(_view is { } view) {
            _goal.Text=Get(view,"goal");var origin=Get(view,"origin");var demo=origin.Contains("SYNTHETIC",StringComparison.OrdinalIgnoreCase)||origin.Contains("DEMO",StringComparison.OrdinalIgnoreCase);
            _context.Text=(demo?"【演示记录 · 非真实GIS执行】 ":"Agent业务计划 · ")+Get(view,"scenario")+" · v"+Get(view,"revision")+"\n"+Get(view,"selectionReason");_context.Foreground=demo?Warn:Muted;
        } else {
            _goal.Text="等待 Agent 发布本次完整业务流程";_context.Text="未提供规范业务记录；旧开发验收快照不当作用户处理流程。";_overview.Text="先列全步骤、方法依据和检查重点，再持续更新实际状态。";
        }
    }
    private void RenderSteps()
    {
        var selected=(_steps.SelectedItem as ListBoxItem)?.Tag as string;_steps.Items.Clear();if(_view is not { } v) { _active.Text="当前无已观测的执行中步骤";return; }
        var running=_rows.Where(r=>Effective(r)=="RUNNING").ToArray();
        _active.Text=running.Length==0?"当前无已观测的执行中步骤（不把最近完成步骤当作执行中）":"▶ 当前执行："+string.Join("；",running.Select(r=>Get(r,"id")+" "+Get(r,"title")+" · "+Kind(Get(r,"kind"))));
        var linear=_rows.Select((r,i)=>i==0?Items(r,"dependsOn").Length==0:Items(r,"dependsOn").Length==1&&Join(r,"dependsOn")==Get(_rows[i-1],"id")).All(x=>x);
        _overview.Text=$"完整步骤 {_rows.Length} 项 · 已核验 {_rows.Count(r=>Effective(r)=="VERIFIED")} · 执行中 {_rows.Count(r=>Effective(r)=="RUNNING")} · 异常/待条件 {_rows.Count(r=>Effective(r) is "FAILED" or "BLOCKED" or "WAITING_USER" or "STALE")} | "+(NewExecution()?"新执行使旧检查待更新":State(Get(v,"candidate")))+" · "+State(Get(v,"acceptance"))+"\n"+(linear?"":"分支清单（依赖详见各步）：")+string.Join(linear?" → ":" / ",_rows.Select(r=>Get(r,"title")));
        foreach(var row in _rows) {
            var state=Effective(row);var card=new StackPanel { Margin=new Thickness(8,5,8,7) };
            card.Children.Add(Text((state=="RUNNING"?"▶ 当前步骤  ":"")+Get(row,"id")+"  "+Get(row,"title"),15,state=="RUNNING"?Accent:Ink));card.Children.Add(Text(Kind(Get(row,"kind"))+" · "+State(state)+" · 依赖 "+Join(row,"dependsOn"),11,Tint(state)));
            if(Get(row,"summary").Length>0)card.Children.Add(Text(Get(row,"summary"),11,Muted));
            if(Join(row,"checkFocus").Length>0)card.Children.Add(Text("检查重点："+Join(row,"checkFocus"),11,Muted));
            if(WorkflowEventBinding.Latest(row,_observed) is { } ev)card.Children.Add(Text("实况 "+ev.ObservedAt.ToLocalTime().ToString("HH:mm:ss")+" · "+ev.Message,11,Accent));
            var ids=Items(row,"requirementIds").Select(x=>x.GetString()).ToHashSet();var checks=Items(v,"checks").Where(c=>ids.Contains(Get(c,"requirementId"))).ToArray();
            if(checks.Length>0)card.Children.Add(Text("检查："+string.Join(" / ",checks.Select(c=>Phase(c)+" "+State(NewExecution()&&Get(c,"effectiveState")=="PASS"?"STALE":Get(c,"effectiveState")))),11,Accent));
            // Execution highlight belongs to the content, independent of selection/manual inspection.
            var executionCard=new Border { Child=card,CornerRadius=new CornerRadius(7),
                Background=state=="RUNNING"?new SolidColorBrush(Color.FromRgb(16,65,63)):Brushes.Transparent,
                BorderBrush=state=="RUNNING"?Accent:Brushes.Transparent,BorderThickness=new Thickness(state=="RUNNING"?4:0,0,0,0) };
            _steps.Items.Add(new ListBoxItem { Tag=Get(row,"id"),Content=executionCard,Background=Brushes.Transparent,BorderThickness=new Thickness(0) });
        }
        var focus=_rows.FirstOrDefault(r=>Effective(r)=="RUNNING");
        if(focus.ValueKind==JsonValueKind.Undefined)focus=_rows.Where(r=>Get(r,"observedAt").Length>0).OrderBy(r=>DateTimeOffset.TryParse(Get(r,"observedAt"),out var at)?at:DateTimeOffset.MinValue).LastOrDefault();
        var focusId=focus.ValueKind==JsonValueKind.Undefined?null:Get(focus,"id");
        var selectedItem=(_follow?_steps.Items.Cast<ListBoxItem>().FirstOrDefault(i=>(string)i.Tag==focusId):_steps.Items.Cast<ListBoxItem>().FirstOrDefault(i=>(string)i.Tag==selected))??_steps.Items.Cast<ListBoxItem>().FirstOrDefault();
        _steps.SelectedItem=selectedItem;if(_follow&&selectedItem is not null)_steps.ScrollIntoView(selectedItem);
    }
    private void RenderDetails()
    {
        _details.Children.Clear();if(_view is not { } v)return;var selected=(_steps.SelectedItem as ListBoxItem)?.Tag as string;
        var r=_rows.FirstOrDefault(r=>Get(r,"id")==selected);if(r.ValueKind==JsonValueKind.Undefined)return;
        _details.Children.Add(Text(Get(r,"title"),19,Ink));_details.Children.Add(Text(Kind(Get(r,"kind"))+" · "+State(Effective(r)),13,Tint(Effective(r))));
        foreach(var p in new[] {("方法与参数",Get(r,"method")),("输入",Join(r,"inputs")),("计划输出（不代表已生成）",Join(r,"outputs")),("检查重点",Join(r,"checkFocus")),("结果",Get(r,"summary")),("证据",Join(r,"evidenceRefs"))})
            if(p.Item2.Length>0) { _details.Children.Add(Text(p.Item1,12,Accent));_details.Children.Add(Text(p.Item2,12,Muted)); }
        var reqs=Items(r,"requirementIds").Select(x=>x.GetString()).ToHashSet();
        foreach(var req in Items(v,"requirements").Where(x=>reqs.Contains(Get(x,"id")))) { _details.Children.Add(Text("判据 · "+Get(req,"title"),13,Ink));_details.Children.Add(Text(Get(req,"criterion")+"\n依据："+Get(req,"source"),12,Muted)); }
        foreach(var c in Items(v,"checks").Where(c=>reqs.Contains(Get(c,"requirementId"))))RenderCheck(_details,c);
        if(WorkflowEventBinding.Latest(r,_observed) is { } ev)_details.Children.Add(Text("内部观测："+ev.Message+"\n"+(ev.Completed.HasValue&&ev.Total>0?$"当前阶段 {ev.Completed}/{ev.Total}；不是整单完成率":"阶段总量未知")+"\n"+ev.Source,12,Accent));
    }
    private void RenderCheck(StackPanel target,JsonElement c)
    {
        var s=NewExecution()&&Get(c,"effectiveState")=="PASS"?"STALE":Get(c,"effectiveState");target.Children.Add(Text(Get(c,"title")+" · "+Phase(c)+" · "+State(s),13,Tint(s)));
        target.Children.Add(Text("判据："+Get(c,"criterion")+"\n结果："+Get(c,"result",Join(c,"evidenceSummary"))+"\n覆盖："+Coverage(c)+"\n依据："+Get(c,"criterionSource"),11,Muted));
    }
    private void RenderQuality()
    {
        _quality.Children.Clear();if(_view is not { } v)return;_quality.Children.Add(Text("检查结果与问题闭环",18,Ink));
        _quality.Children.Add(Text(NewExecution()?"发现较新执行：旧通过项失效，等待重读/复检":"技术候选："+State(Get(v,"candidate"))+"\n外部验收："+State(Get(v,"acceptance")),12,Accent));
        foreach(var issue in Items(v,"issues")) {
            _quality.Children.Add(Text(Get(issue,"id")+" · "+State(Get(issue,"status"))+" · "+Get(issue,"severity"),14,Warn));_quality.Children.Add(Text(Get(issue,"description"),12,Ink));
            if(issue.TryGetProperty("repair",out var repair))_quality.Children.Add(Text("当前处置："+State(Get(repair,"state"))+"\n"+Get(repair,"action")+"\n受影响步骤："+Join(repair,"affectedStepIds")+"\n复检："+Get(repair,"recheckResult"),12,Accent));
            _quality.Children.Add(Text("修复/复检证据："+Join(issue,"resolutionEvidenceRefs"),11,Muted));
        }
        if(Items(v,"issues").Length==0)_quality.Children.Add(Text("记录未列问题；不等于全部检查通过。",11,Muted));
        foreach(var c in Items(v,"checks"))RenderCheck(_quality,c);
        _quality.Children.Add(Text("适用依据与局限",14,Accent));foreach(var s in Items(v,"standards"))_quality.Children.Add(Text(s.ValueKind==JsonValueKind.Object?Get(s,"source")+" · "+Get(s,"scope"):s.ToString(),11,Muted));foreach(var s in Items(v,"constraints"))_quality.Children.Add(Text(s.ToString(),11,Warn));
        _quality.Children.Add(Text($"保留检查历史 {Items(v,"qualityHistory").Length} 项，改计划记录 {Items(v,"changes").Length} 项。",11,Muted));
        foreach(var old in Items(v,"qualityHistory").TakeLast(10))_quality.Children.Add(Text("历史检查："+old.ToString(),11,Muted));
        foreach(var change in Items(v,"changes").TakeLast(10))_quality.Children.Add(Text("方案变更："+change.ToString(),11,Warn));
        _quality.Children.Add(Text("面板不执行修复，不从工具成功自批质量或外部验收。",11,Accent));
    }
    private void RenderIntake()
    {
        _intake.Children.Clear();if(_view is not { } v) { _intakeSummary.Text="资料和成果位置尚未记录";return; }
        _intake.Children.Add(Text("资料、工具方案与详细交付位置",18,Ink));
        if(v.TryGetProperty("intake",out var intake)&&Get(intake,"status")!="LEGACY_UNCONFIRMED") {
            var root=intake.TryGetProperty("outputRoot",out var output)?Get(output,"value"):"待提供";
            _intakeSummary.Text="资料确认："+(Get(intake,"status")=="READY"?"已齐备":"等待用户补充/确认")+" · 成果目录："+root;
            foreach(var entry in new[] {("requirements","用户需求"),("inputs","数据位置"),("outputRoot","成果保存位置")}) {
                if(!intake.TryGetProperty(entry.Item1,out var row))continue;
                _intake.Children.Add(Text(entry.Item2+" · "+Get(row,"status"),13,Accent));
                _intake.Children.Add(Text(entry.Item1=="inputs"?Join(row,"value"):Get(row,"value","待提供"),12,Ink));
            }
            foreach(var q in Items(intake,"questions"))_intake.Children.Add(Text("请在对话中答复："+q.ToString(),12,Warn));
            foreach(var gap in Items(intake,"dataNeeds")) {
                _intake.Children.Add(Text("补充资料："+Get(gap,"label")+" · "+Get(gap,"state"),14,Warn));
                _intake.Children.Add(Text("用途："+Get(gap,"reason")+"\n补充建议："+Get(gap,"recommendation")+"\n影响步骤："+Join(gap,"stepIds"),12,Muted));
            }
            foreach(var choice in Items(intake,"toolChoices")) {
                _intake.Children.Add(Text("工具方案 "+Get(choice,"stepId")+" · "+Get(choice,"status"),14,Accent));
                _intake.Children.Add(Text(Join(choice,"tools")+"\n"+Get(choice,"reason"),12,Ink));
                foreach(var field in new[] {("equivalence","等效条件"),("limits","差异与限制"),("alternativesConsidered","已评估替代方案")})
                    if(Get(choice,field.Item1).Length>0)_intake.Children.Add(Text(field.Item2+"："+Get(choice,field.Item1),12,Muted));
            }
        } else { _intakeSummary.Text="旧记录未确认本任务的需求、数据和成果目录";_intake.Children.Add(Text(_intakeSummary.Text,12,Warn)); }
        _intake.Children.Add(Text("实际成果清单（计划路径不代表已生成）",14,Accent));
        foreach(var artifact in Items(v,"artifacts"))_intake.Children.Add(Text(Get(artifact,"label")+" · "+State(Get(artifact,"state"))+"\n"+Get(artifact,"path","尚无路径"),12,Get(artifact,"state")=="VERIFIED"?Accent:Muted));
        _intake.Children.Add(Text("请在聊天中回答或修改需求；本面板只读显示。开发须用户明确同意，现有工具替代优先；同意开发不表示已经可执行。",11,Muted));
    }
    private void RenderEvents()
    {
        _events.Children.Clear();var last=_observed.LastOrDefault();if(last is null) { _internal.Text="尚无内部事件";return; }
        _internal.Text=State(last.State)+" · "+last.Tool+"\n"+last.Message+"\n"+last.ObservedAt.ToLocalTime().ToString("HH:mm:ss.fff")+" · "+last.RequestId+" · "+last.Source;
        foreach(var e in _observed.TakeLast(80).Reverse()) { _events.Children.Add(Text(e.ObservedAt.ToLocalTime().ToString("HH:mm:ss.fff")+" "+State(e.State)+" "+e.Tool,11,Accent));_events.Children.Add(Text(e.Message+"\n"+(e.Total>0?$"当前阶段 {e.Completed}/{e.Total}，非整单完成率\n":"")+e.Source+" · "+e.RequestId,11,Muted)); }
    }
    private void Freshness()
    {
        if(_view is not { } v) { _freshness.Text="没有业务任务记录；内部日志可单独查看。";return; }
        var bound=_rows.Select(r=>WorkflowEventBinding.Latest(r,_observed)).Where(e=>e is not null).OrderBy(e=>e!.ObservedAt).LastOrDefault();var raw=Get(v,"observedAt");
        var age=DateTimeOffset.TryParse(raw,out var observed)?(DateTimeOffset.UtcNow-observed).TotalSeconds:double.NaN;
        _freshness.Text="任务最近观测："+(raw.Length>0?raw:"尚无实际观测")+" · 本任务SDK："+(bound?.ObservedAt.ToLocalTime().ToString("HH:mm:ss.fff")??"尚无绑定事件")+(age>15?" · 暂无新记录，保留最后状态，不推断仍在执行":"")+(_journalError() is { } error?" · 日志不可用："+error:"");
    }
}

