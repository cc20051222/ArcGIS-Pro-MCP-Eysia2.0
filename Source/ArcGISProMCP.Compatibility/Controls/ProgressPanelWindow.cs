using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Markup;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Progress;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using FontFamily = System.Windows.Media.FontFamily;
using ProgressBar = System.Windows.Controls.ProgressBar;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
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
    private readonly string _sourceLabel;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _goal,_context,_overview,_freshness,_internal,_active,_intakeSummary;
    private readonly ListBox _steps;
    private readonly StackPanel _details,_quality,_events,_intake;
    private JsonElement? _view;
    private JsonElement[] _rows=System.Array.Empty<JsonElement>();
    private IReadOnlyList<ToolProgressEvent> _observed=System.Array.Empty<ToolProgressEvent>();
    private IReadOnlyList<ToolProgressEvent> _renderedEvents=System.Array.Empty<ToolProgressEvent>();
    private readonly Dictionary<string,ToolProgressEvent?> _latestByStep=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _effectiveByStep=new(StringComparer.Ordinal);
    private readonly HashSet<string> _boundRequests=new(StringComparer.Ordinal);
    private readonly Dictionary<string,(ToolProgressEvent Event,long Order)> _executionMemory=new(StringComparer.Ordinal);
    private long _executionOrder;
    private bool _paused;
    private bool _follow=true;
    private bool _checksStale;
    private readonly Button _followButton;
    private readonly TextBlock _stageText,_flowHint;
    private readonly TextBlock _watchState,_searchHint,_emptySteps,_emptyHint;
    private readonly TextBlock[] _statistics=new TextBlock[4];
    private readonly TextBox _stepSearch;
    private readonly System.Windows.Controls.Primitives.ToggleButton _attentionOnly;
    private readonly Button _qualityButton;
    private readonly TabControl _tabs;
    private readonly ScrollViewer _detailScroll;
    private readonly StackPanel _emptyList;
    private readonly DispatcherTimer _searchTimer;
    private bool _renderingSteps,_resetListScroll;
    private int _renderGeneration;
    private int _artifactPathSerial;
    private string _taskIdentity="",_recordIssue="",_detailSelection="";
    private readonly ProgressBar _stageProgress;
    private readonly Dictionary<string,bool> _expanded=new();
    private sealed record StepAppearance(string Title,string Kind,string State,string Note,int Index);
    private readonly Dictionary<string,(StepAppearance Appearance,ListBoxItem Item)> _stepItems=new(StringComparer.Ordinal);
    private readonly WorkflowBackdrop _backdrop,_scenePreview;
    private readonly DispatcherTimer _ambientTimer,_sceneryTimer;
    private readonly Func<DateTime> _sceneryClock;
    private readonly System.Diagnostics.Stopwatch _ambientClock=new();
    private readonly TextBlock _sceneCaption,_scenerySchedule;
    private readonly bool _sceneryClockIsFixture;
    private string _sceneryTheme="";
    private string _recordFingerprint="";
    private string? _eventIssue,_renderedEventIssue;
    private static Color PaintColor(string hex)=>(Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,Brush> Palette=new();
    private static Brush Paint(string hex)=>Palette.GetOrAdd(hex,static h=> { var b=new SolidColorBrush(PaintColor(h));b.Freeze();return b; });
    private static readonly Brush Ink=Paint("#203D35"),Muted=Paint("#4E665A"),Accent=Paint("#225E49"),
        Warn=Paint("#785016"),Danger=Paint("#963B3D"),Line=Paint("#D8E1D8"),Canvas=Paint("#E8EEE8");
    public ProgressPanelWindow(Func<IReadOnlyList<ToolProgressEvent>> read,Func<string?>? journalError=null,
        string? taskRecord=null,string sourceLabel="当前 Pro 进程",Func<DateTime>? sceneryClock=null)
    {
        _read=read;_journalError=journalError??(()=>null);_taskRecord=taskRecord;_sourceLabel=sourceLabel;
        _sceneryClock=sceneryClock??(()=>DateTime.Now);_sceneryClockIsFixture=sceneryClock is not null;
        _searchTimer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(180) };
        _searchTimer.Tick+=(_,_)=> { _searchTimer.Stop();RenderSteps();RenderDetails(); };
        Title="ArcGIS Pro MCP · 任务与质量面板";Width=1400;Height=900;MinWidth=960;MinHeight=640;
        Background=Canvas;Foreground=Ink;FontSize=13;
        FontFamily=new FontFamily("Microsoft YaHei UI");WindowStartupLocation=WindowStartupLocation.CenterScreen;
        UseLayoutRounding=true;SnapsToDevicePixels=true;
        Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Theme));
        var stage=new Grid { Background=Canvas };Content=stage;
        // A quiet scenic margin and one continuous reading surface replace stacked glass cards.
        _backdrop=new WorkflowBackdrop { Opacity=.07 };AutomationProperties.SetAutomationId(_backdrop,"SceneryBackground");stage.Children.Add(_backdrop);
        var layout=new Grid();layout.ColumnDefinitions.Add(new() { Width=new GridLength(280) });layout.ColumnDefinitions.Add(new());stage.Children.Add(layout);
        _scenePreview=new WorkflowBackdrop();AutomationProperties.SetAutomationId(_scenePreview,"SceneryPreview");
        var landscape=new Grid { ClipToBounds=true };landscape.Children.Add(_scenePreview);
        landscape.Children.Add(new Border { IsHitTestVisible=false,Background=new LinearGradientBrush(new GradientStopCollection {
            new(PaintColor("#B809302C"),0),new(PaintColor("#6B0E3B35"),.36),new(PaintColor("#00000000"),.62),new(PaintColor("#90072F2E"),1) },new Point(0,0),new Point(0,1)) });
        var sceneType=new StackPanel { Margin=new Thickness(24,28,24,0),VerticalAlignment=VerticalAlignment.Top };
        sceneType.Children.Add(Text("ARCGIS PRO  /  MCP",11,Brushes.White));
        var wordmark=Text("山海\n工作台",38,Brushes.White);wordmark.FontFamily=new FontFamily("SimSun");wordmark.Margin=new Thickness(0,30,0,14);sceneType.Children.Add(wordmark);
        sceneType.Children.Add(Text("每一步，都有迹可循",12,Brushes.White));landscape.Children.Add(sceneType);
        _scenerySchedule=Text("自动景观",12,Brushes.White);AutomationProperties.SetAutomationId(_scenerySchedule,"ScenerySchedule");
        _scenerySchedule.ToolTip="本机时间：05–08 晨曦，08–17 日间，17–20 暮色，20–05 夜间。装饰独立于 GIS 进度，遵循系统减少动画设置。";
        _sceneCaption=Text("装饰景观 · 非任务地图",10,Brushes.White);
        var sceneNote=new StackPanel();sceneNote.Children.Add(_scenerySchedule);sceneNote.Children.Add(_sceneCaption);
        landscape.Children.Add(new Border { Child=sceneNote,Background=Paint("#B8083430"),Padding=new Thickness(14,10,14,10),CornerRadius=new CornerRadius(12),Margin=new Thickness(18),VerticalAlignment=VerticalAlignment.Bottom });
        var frame=new Border { Child=landscape,CornerRadius=new CornerRadius(20),Margin=new Thickness(16,16,12,16) };
        frame.SizeChanged+=(_,e)=>landscape.Clip=new RectangleGeometry(new Rect(0,0,e.NewSize.Width,e.NewSize.Height),20,20);layout.Children.Add(frame);
        SizeChanged+=(_,_)=>layout.ColumnDefinitions[0].Width=new GridLength(ActualWidth<1160?220:ActualWidth>=1400?300:280);
        var root=new Grid();for(var i=0;i<4;i++)root.RowDefinitions.Add(new() { Height=i==2?new GridLength(1,GridUnitType.Star):GridLength.Auto });
        var paper=new Border { Child=root,Background=Paint("#F6FAFBF6"),CornerRadius=new CornerRadius(20),Padding=new Thickness(26,20,26,16),Margin=new Thickness(0,16,16,16) };Grid.SetColumn(paper,1);layout.Children.Add(paper);
        var header=new DockPanel { Margin=new Thickness(0,0,0,16) };
        var actions=new StackPanel { Orientation=Orientation.Horizontal };
        _followButton=new Button { Content="跟随当前 · 开",Margin=new Thickness(0,0,8,0),ToolTip="自动定位当前执行步骤；手动浏览不改变执行状态。" };
        AutomationProperties.SetAutomationId(_followButton,"FollowCurrentStep");_followButton.Click+=(_,_)=> { _follow=!_follow;if(_follow) { ResetFilters();_follow=true; }UpdateFollowButton();RenderSteps();RenderDetails(); };
        var pause=new Button { Content="暂停刷新",ToolTip="仅暂停观测，不取消 GIS 任务。" };AutomationProperties.SetAutomationId(pause,"PauseObservation");
        pause.Click+=(_,_)=> { _paused=!_paused;pause.Content=_paused?"继续刷新":"暂停刷新";Refresh(); };
        actions.Children.Add(_followButton);actions.Children.Add(pause);DockPanel.SetDock(actions,Dock.Right);header.Children.Add(actions);
        var section=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
        section.Children.Add(Text("任务概览",13,Accent));_watchState=Text("等待流程",10,Muted);AutomationProperties.SetAutomationId(_watchState,"ObservationState");
        section.Children.Add(new Border { Child=_watchState,Background=Paint("#EBF0E6"),CornerRadius=new CornerRadius(8),Padding=new Thickness(8,0,8,0),Margin=new Thickness(12,0,0,0) });header.Children.Add(section);root.Children.Add(header);
        var summary=new StackPanel();
        _context=OneLine("业务流程 · 持续观测",10,Muted);
        AutomationProperties.SetAutomationId(_context,"WorkflowContext");
        _goal=Text("等待发布任务流程",22,Ink);_goal.FontWeight=FontWeights.SemiBold;_goal.MaxHeight=64;_goal.TextTrimming=TextTrimming.CharacterEllipsis;
        AutomationProperties.SetAutomationId(_goal,"TaskGoal");
        summary.Children.Add(_goal);summary.Children.Add(_context);
        var current=new StackPanel();var executionLine=new DockPanel();var currentLabel=Text("当前执行",10,Accent);currentLabel.Width=58;currentLabel.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(currentLabel,Dock.Left);executionLine.Children.Add(currentLabel);
        _active=Text("当前无已观测的执行中步骤",15,Ink);_active.FontWeight=FontWeights.SemiBold;_active.MaxHeight=48;_active.TextTrimming=TextTrimming.CharacterEllipsis;executionLine.Children.Add(_active);current.Children.Add(executionLine);
        AutomationProperties.SetAutomationId(_active,"ActiveStep");
        _stageText=OneLine("尚无阶段计数",11,Muted);_stageText.Margin=new Thickness(58,3,0,3);current.Children.Add(_stageText);
        AutomationProperties.SetAutomationId(_stageText,"StageDescription");
        _stageProgress=new ProgressBar { Height=3,Foreground=Accent,Background=Line,BorderThickness=new Thickness(0),Visibility=Visibility.Collapsed,Margin=new Thickness(0,7,0,0) };current.Children.Add(_stageProgress);
        var currentPanel=new Border { Child=current,Background=Paint("#E8F0E9"),CornerRadius=new CornerRadius(12),Padding=new Thickness(14,8,14,9),Margin=new Thickness(0,12,0,10) };summary.Children.Add(currentPanel);
        _intakeSummary=OneLine("需求、数据与成果位置待确认",10,Warn);summary.Children.Add(_intakeSummary);
        AutomationProperties.SetAutomationId(_intakeSummary,"IntakeSummary");
        var metrics=new Grid { Margin=new Thickness(0,9,0,0) };
        var labels=new[] {"全部步骤","已核验","执行中","需关注步骤"};var ids=new[] {"StatTotal","StatVerified","StatRunning","StatAttention"};
        for(var i=0;i<4;i++) {
            metrics.ColumnDefinitions.Add(new());var cell=new StackPanel { Orientation=Orientation.Horizontal };_statistics[i]=Text("—",20,i==3?Warn:Accent);_statistics[i].FontWeight=FontWeights.SemiBold;
            AutomationProperties.SetAutomationId(_statistics[i],ids[i]);cell.Children.Add(_statistics[i]);var metricLabel=Text(labels[i],10,Muted);metricLabel.Margin=new Thickness(7,0,0,0);metricLabel.VerticalAlignment=VerticalAlignment.Center;cell.Children.Add(metricLabel);Grid.SetColumn(cell,i);metrics.Children.Add(cell);
            AutomationProperties.SetHelpText(_statistics[i],labels[i]+"；全任务数量，不随列表筛选改变。");
        }
        summary.Children.Add(metrics);
        var review=new DockPanel { Margin=new Thickness(0,7,0,0) };_qualityButton=new Button { Content="查看检查与修复",FontSize=10,Padding=new Thickness(9,4,9,4) };AutomationProperties.SetAutomationId(_qualityButton,"OpenQuality");_qualityButton.Click+=(_,_)=>OpenQuality();
        DockPanel.SetDock(_qualityButton,Dock.Right);review.Children.Add(_qualityButton);_overview=OneLine("完整步骤将在这里显示",10,Muted);_overview.VerticalAlignment=VerticalAlignment.Center;review.Children.Add(_overview);summary.Children.Add(review);
        summary.Margin=new Thickness(0,0,0,12);Grid.SetRow(summary,1);root.Children.Add(summary);
        var body=new Grid();body.ColumnDefinitions.Add(new() { Width=new GridLength(.48,GridUnitType.Star),MinWidth=250 });body.ColumnDefinitions.Add(new() { Width=new GridLength(28) });body.ColumnDefinitions.Add(new() { Width=new GridLength(.52,GridUnitType.Star),MinWidth=280 });
        var rule=new Border { Background=Line,Width=1,Margin=new Thickness(0,0,0,0) };Grid.SetColumn(rule,1);body.Children.Add(rule);
        var left=new Grid();left.RowDefinitions.Add(new() { Height=GridLength.Auto });left.RowDefinitions.Add(new());
        var flowHeader=new StackPanel { Margin=new Thickness(0,0,0,8) };
        var flowTitle=new DockPanel();var locate=new Button { Content="定位当前",FontSize=10,Padding=new Thickness(8,4,8,4),ToolTip="清除列表筛选并恢复跟随；不会启动或更改 GIS。" };AutomationProperties.SetAutomationId(locate,"LocateCurrentStep");locate.Click+=(_,_)=> { ResetFilters();_follow=true;UpdateFollowButton();RenderSteps();RenderDetails(); };
        DockPanel.SetDock(locate,Dock.Right);flowTitle.Children.Add(locate);var title=Text("处理流程",15,Ink);title.FontWeight=FontWeights.SemiBold;flowTitle.Children.Add(title);flowHeader.Children.Add(flowTitle);
        var filters=new Grid { Margin=new Thickness(0,9,0,4) };filters.ColumnDefinitions.Add(new());filters.ColumnDefinitions.Add(new() { Width=GridLength.Auto });filters.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        var searchArea=new Grid();_stepSearch=new TextBox { Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(8,5,8,5),FontSize=11,Foreground=Ink,MaxLength=200,VerticalContentAlignment=VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(_stepSearch,"StepSearch");AutomationProperties.SetName(_stepSearch,"搜索步骤、编号或状态");searchArea.Children.Add(_stepSearch);
        _searchHint=OneLine("搜索步骤 / 编号 / 状态",10,Muted);_searchHint.IsHitTestVisible=false;_searchHint.Margin=new Thickness(8,0,0,0);_searchHint.VerticalAlignment=VerticalAlignment.Center;searchArea.Children.Add(_searchHint);
        filters.Children.Add(new Border { Child=searchArea,Background=Paint("#EEF3EA"),BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8) });
        _attentionOnly=new System.Windows.Controls.Primitives.ToggleButton { Content="需关注",FontSize=10,Padding=new Thickness(8,5,8,5),Margin=new Thickness(6,0,0,0),Style=(Style)Resources["FilterToggle"] };
        AutomationProperties.SetAutomationId(_attentionOnly,"AttentionOnly");_attentionOnly.ToolTip="筛选失败、受阻、等待用户、失效及待核查步骤；全任务计数不受筛选影响。";Grid.SetColumn(_attentionOnly,1);filters.Children.Add(_attentionOnly);
        var clear=new Button { Content="清空",FontSize=10,Padding=new Thickness(6,5,6,5),Margin=new Thickness(4,0,0,0),ToolTip="显示全部步骤" };AutomationProperties.SetAutomationId(clear,"ClearStepFilters");clear.Click+=(_,_)=> { ResetFilters();RenderSteps();RenderDetails(); };Grid.SetColumn(clear,2);filters.Children.Add(clear);flowHeader.Children.Add(filters);
        _flowHint=OneLine("点击步骤查看详情",10,Muted);AutomationProperties.SetAutomationId(_flowHint,"StepFilterStatus");flowHeader.Children.Add(_flowHint);left.Children.Add(flowHeader);
        _stepSearch.TextChanged+=(_,_)=> { _searchHint.Visibility=_stepSearch.Text.Length==0?Visibility.Visible:Visibility.Collapsed;QueueFilter(); };
        _attentionOnly.Checked+=(_,_)=>QueueFilter();_attentionOnly.Unchecked+=(_,_)=>QueueFilter();
        PreviewKeyDown+=(_,e)=> { if(e.Key==Key.F&&Keyboard.Modifiers==ModifierKeys.Control) { _stepSearch.Focus();_stepSearch.SelectAll();e.Handled=true; }else if(e.Key==Key.Escape&&(_stepSearch.Text.Length>0||_attentionOnly.IsChecked==true)) { ResetFilters();RenderSteps();RenderDetails();e.Handled=true; } };
        _steps=new ListBox { Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Ink,HorizontalContentAlignment=System.Windows.HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(_steps,"WorkflowSteps");
        VirtualizingPanel.SetIsVirtualizing(_steps,true);VirtualizingPanel.SetVirtualizationMode(_steps,VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_steps,true);
        var itemTemplate=new ControlTemplate(typeof(ListBoxItem));var border=new FrameworkElementFactory(typeof(Border));border.Name="StepCard";
        border.SetValue(Border.BackgroundProperty,Brushes.Transparent);border.SetValue(Border.BorderBrushProperty,Brushes.Transparent);border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));border.AppendChild(presenter);itemTemplate.VisualTree=border;
        var selected=new Trigger { Property=ListBoxItem.IsSelectedProperty,Value=true };selected.Setters.Add(new Setter(Border.BackgroundProperty,Paint("#EDF2E9"),"StepCard"));itemTemplate.Triggers.Add(selected);
        var focus=new Trigger { Property=ListBoxItem.IsKeyboardFocusWithinProperty,Value=true };focus.Setters.Add(new Setter(Border.BorderBrushProperty,Accent,"StepCard"));itemTemplate.Triggers.Add(focus);
        _steps.ItemContainerStyle=new Style(typeof(ListBoxItem)) { Setters={new Setter(Control.TemplateProperty,itemTemplate),new Setter(FrameworkElement.HorizontalAlignmentProperty,System.Windows.HorizontalAlignment.Left),new Setter(Control.HorizontalContentAlignmentProperty,System.Windows.HorizontalAlignment.Stretch),new Setter(FrameworkElement.MarginProperty,new Thickness(0,0,0,4))} };
        ScrollViewer.SetHorizontalScrollBarVisibility(_steps,ScrollBarVisibility.Disabled);_steps.SelectionChanged+=(_,_)=> { if(!_renderingSteps)RenderDetails(); };
        var stepArea=new Grid();stepArea.Children.Add(_steps);_emptyList=new StackPanel { VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,20,8,20),IsHitTestVisible=false };
        _emptySteps=Text("等待任务流程",15,Ink);_emptyHint=Text("在对话中确认需求、数据与成果位置后，Agent 将同步完整步骤。",11,Muted);_emptyList.Children.Add(_emptySteps);_emptyList.Children.Add(_emptyHint);AutomationProperties.SetAutomationId(_emptyList,"StepEmptyState");stepArea.Children.Add(_emptyList);Grid.SetRow(stepArea,1);left.Children.Add(stepArea);body.Children.Add(left);
        _steps.PreviewMouseDown+=(_,_)=>StopFollowing();_steps.PreviewKeyDown+=(_,e)=> { if(e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)StopFollowing(); };
        _tabs=new TabControl { Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,12,0,0) };var tabs=_tabs;AutomationProperties.SetAutomationId(tabs,"WorkflowDetails");
        _details=new();_quality=new();_events=new();_intake=new();
        _detailScroll=Scroll(_details);AutomationProperties.SetAutomationId(_detailScroll,"StepDetailScroll");tabs.Items.Add(new TabItem { Header="步骤详情",Content=_detailScroll });tabs.Items.Add(new TabItem { Header="检查修复",Content=Scroll(_quality) });
        var internals=new StackPanel();_internal=Text("等待内部事件",12,Muted);internals.Children.Add(_internal);internals.Children.Add(_events);
        AutomationProperties.SetAutomationId(_internal,"InternalSummary");
        tabs.Items.Add(new TabItem { Header="内部进度",Content=Scroll(internals) });tabs.Items.Add(new TabItem { Header="资料交付",Content=Scroll(_intake) });Grid.SetColumn(tabs,2);body.Children.Add(tabs);
        Grid.SetRow(body,2);root.Children.Add(body);
        var footer=new Grid { Margin=new Thickness(0,12,0,0) };footer.ColumnDefinitions.Add(new());footer.ColumnDefinitions.Add(new());
        footer.Children.Add(OneLine("只读观测 · 500 ms · 关闭不取消任务",10,Muted));_freshness=OneLine("尚无业务观测",10,Muted);_freshness.ToolTip=sourceLabel;_freshness.TextAlignment=TextAlignment.Right;Grid.SetColumn(_freshness,1);footer.Children.Add(_freshness);
        Grid.SetRow(footer,3);root.Children.Add(footer);
        _timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(MCPSettings.ProgressPanelRefreshMilliseconds) };
        _ambientTimer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds((RenderCapability.Tier>>16)>=2?66:125) };
        _ambientTimer.Tick+=(_,_)=> { var t=_ambientClock.Elapsed.TotalSeconds;_scenePreview.SetFrame(t); };
        _sceneryTimer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromSeconds(_sceneryClockIsFixture?1:30) };
        _sceneryTimer.Tick+=(_,_)=>UpdateScenery();
        _timer.Tick+=(_,_)=>Refresh();Loaded+=(_,_)=> { Refresh();_timer.Start();_ambientClock.Start();UpdateScenery();_sceneryTimer.Start();UpdateAmbient(); };
        Activated+=(_,_)=> { UpdateScenery();UpdateAmbient(); };Deactivated+=(_,_)=>UpdateAmbient();StateChanged+=(_,_)=>UpdateAmbient();
        SystemParameters.StaticPropertyChanged+=SystemMotionChanged;
        Closed+=(_,_)=> { _timer.Stop();_ambientTimer.Stop();_sceneryTimer.Stop();_searchTimer.Stop();_ambientClock.Stop();SystemParameters.StaticPropertyChanged-=SystemMotionChanged; };
        bool? compactDensity=null;
        void UpdateDensity()
        {
            var compact=ActualHeight<760;if(compactDensity==compact)return;compactDensity=compact;
            paper.Padding=compact?new Thickness(18,12,18,12):new Thickness(26,20,26,16);
            header.Margin=new Thickness(0,0,0,compact?8:16);summary.Margin=new Thickness(0,0,0,compact?8:12);
            currentPanel.Margin=compact?new Thickness(0,8,0,6):new Thickness(0,12,0,10);
            currentPanel.Padding=compact?new Thickness(14,6,14,6):new Thickness(14,8,14,9);
            metrics.Margin=new Thickness(0,compact?4:9,0,0);review.Margin=new Thickness(0,compact?4:7,0,0);
            footer.Margin=new Thickness(0,compact?8:12,0,0);flowHeader.Margin=new Thickness(0,0,0,compact?6:8);
            _goal.FontSize=compact?20:22;_active.FontSize=compact?14:15;
            foreach(var metric in _statistics)metric.FontSize=compact?18:20;
            wordmark.FontSize=compact?34:38;
            AutomationProperties.SetItemStatus(this,compact?"density:compact":"density:comfortable");
        }
        SizeChanged+=(_,_)=>UpdateDensity();Loaded+=(_,_)=>UpdateDensity();
    }
    private static TextBlock Text(string v,double size,Brush color)=>new() { Text=v,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3) };
    private static TextBlock OneLine(string value,double size,Brush color)=>new() { Text=value,ToolTip=value,FontSize=size,Foreground=color,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,3) };
    // Small quality cells sit on the continuous paper surface; scenery never crosses body text.
    private static Border Card(UIElement content,double padding)=>new() { Child=content,Background=Paint("#EDF2EA"),CornerRadius=new CornerRadius(10),Padding=new Thickness(padding) };
    private void UpdateScenery()
    {
        if(_scenerySchedule is null)return;
        var (theme,label)=WorkflowBackdrop.ForLocalTime(_sceneryClock());
        if(theme==_sceneryTheme)return;
        _sceneryTheme=theme;_backdrop.SetTheme(theme);_scenePreview.SetTheme(theme,_ambientTimer is not null&&_ambientTimer.IsEnabled);
        _scenerySchedule.Text=label+" · 自动"+(_sceneryClockIsFixture?"（时间演示）":"");
        _sceneCaption.Text="装饰景观 · 非任务地图"+(_sceneryClockIsFixture?" · 时间演示":"");
    }
    private void SystemMotionChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    { if(e.PropertyName==nameof(SystemParameters.ClientAreaAnimation))Dispatcher.BeginInvoke(new Action(UpdateAmbient)); }
    private void UpdateAmbient()
    {
        if(_ambientTimer is null)return;
        var supported=SystemParameters.ClientAreaAnimation&&(RenderCapability.Tier>>16)>0;
        if(supported&&IsVisible&&IsActive&&WindowState!=System.Windows.WindowState.Minimized) { _ambientClock.Start();_ambientTimer.Start(); }
        else { _ambientTimer.Stop();_ambientClock.Stop();_scenePreview.FinishTransition(); }
    }
    private void OpenQuality()=>_tabs.SelectedIndex=1;
    private void QueueFilter()
    { StopFollowing();_resetListScroll=true;_searchTimer.Stop();_searchTimer.Start(); }
    private void ResetFilters()
    { _stepSearch.Clear();_attentionOnly.IsChecked=false;_searchTimer.Stop();_resetListScroll=true; }
    private static T? VisualChild<T>(DependencyObject parent) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var child=VisualTreeHelper.GetChild(parent,i);if(child is T match)return match;if(VisualChild<T>(child) is { } nested)return nested; }
        return null;
    }
    private static bool Attention(string state)=>state is "FAILED" or "BLOCKED" or "WAITING_USER" or "STALE" or "UNKNOWN" or "REPAIRING" or "RECHECKING" or "CANCEL_REQUESTED";
    private void Statistics(params int[] values)
    { for(var i=0;i<_statistics.Length;i++)_statistics[i].Text=i<values.Length?values[i].ToString():"—";_statistics[3].Foreground=values.Length>3&&values[3]>0?Warn:Muted; }
    private void EmptyList(bool shown,string title="没有匹配的步骤",string hint="调整关键词或清空筛选可显示全部步骤；任务计数保持完整。")
    { _emptyList.Visibility=shown?Visibility.Visible:Visibility.Collapsed;_emptySteps.Text=title;_emptyHint.Text=hint; }
    private void EmptyDetails(string title,string hint)
    { _details.Children.Clear();_details.Children.Add(Text(title,18,Ink));_details.Children.Add(Text(hint,12,Muted)); }
    private static ScrollViewer Scroll(UIElement content)=>new() { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,8,0,0),Padding=new Thickness(0,0,6,0) };
    private void UpdateFollowButton()=>_followButton.Content=_follow?"跟随当前 · 开":"跟随当前 · 关";
    private void StopFollowing() { _follow=false;UpdateFollowButton(); }
    private Expander Fold(string title,UIElement content,string key,bool open=false)
    {
        var fold=new Expander { Header=title,Content=content,IsExpanded=_expanded.TryGetValue(key,out var value)?value:open,Margin=new Thickness(0,10,0,5),Foreground=Ink,FontSize=12 };
        fold.Expanded+=(_,_)=>_expanded[key]=true;fold.Collapsed+=(_,_)=>_expanded[key]=false;return fold;
    }
    private static void Field(StackPanel target,string label,string value)
    { if(value.Length==0)return;var heading=Text(label,11,Muted);heading.Margin=new Thickness(0,12,0,3);target.Children.Add(heading);target.Children.Add(Text(value,12,Ink)); }
    private const string Theme="""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style x:Key="FilterToggle" TargetType="ToggleButton">
            <Setter Property="Foreground" Value="#4E665A"/><Setter Property="Background" Value="#F2F5EE"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ToggleButton">
              <Border x:Name="Pill" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="#D8E1D8" BorderThickness="1" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="Pill" Property="Background" Value="#DFEBD9"/><Setter Property="Foreground" Value="#225E49"/><Setter TargetName="Pill" Property="BorderBrush" Value="#225E49"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Pill" Property="Background" Value="#E6EEE3"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Pill" Property="BorderBrush" Value="#225E49"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="10"/><Setter Property="Background" Value="Transparent"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
              <Track x:Name="PART_Track" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" ViewportSize="{TemplateBinding ViewportSize}">
                <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False" IsTabStop="False"/></Track.DecreaseRepeatButton>
                <Track.Thumb><Thumb MinHeight="24" Margin="2,0"><Thumb.Template><ControlTemplate TargetType="Thumb"><Border x:Name="Handle" CornerRadius="3" Background="#BDCCBB"/><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Handle" Property="Background" Value="#6E8A73"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False" IsTabStop="False"/></Track.IncreaseRepeatButton>
              </Track>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Expander">
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Expander">
              <StackPanel>
                <ToggleButton Content="{TemplateBinding Header}" Foreground="{TemplateBinding Foreground}" HorizontalContentAlignment="Left" IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent}}" Background="Transparent" BorderThickness="0">
                  <ToggleButton.Template><ControlTemplate TargetType="ToggleButton">
                    <Grid Background="Transparent"><Grid.ColumnDefinitions><ColumnDefinition Width="15"/><ColumnDefinition/></Grid.ColumnDefinitions>
                      <Path x:Name="Chevron" Data="M 2 2 L 6 6 L 2 10" Stroke="{TemplateBinding Foreground}" StrokeThickness="1.2" VerticalAlignment="Center" RenderTransformOrigin="0.5,0.5"/>
                      <ContentPresenter Grid.Column="1" VerticalAlignment="Center" Margin="2,3,0,3"/>
                    </Grid>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsChecked" Value="True"><Setter TargetName="Chevron" Property="RenderTransform"><Setter.Value><RotateTransform Angle="90"/></Setter.Value></Setter></Trigger>
                      <Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Foreground" Value="#153F2E"/></Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate></ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter x:Name="Body" ContentSource="Content" Visibility="Collapsed" Margin="17,5,0,0"/>
              </StackPanel>
              <ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Body" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Button">
            <Setter Property="Foreground" Value="#225E49"/><Setter Property="Background" Value="#F2F5EE"/>
            <Setter Property="BorderBrush" Value="#DBE3D9"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="12,7"/><Setter Property="FontSize" Value="12"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
              <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="15" Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="#E6EEE3"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="#225E49"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="#DCE9DA"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TabItem">
            <Setter Property="Foreground" Value="#4E665A"/><Setter Property="Padding" Value="7,8"/><Setter Property="FontSize" Value="12"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem">
              <Border x:Name="Tab" Padding="{TemplateBinding Padding}" CornerRadius="8" Margin="0,0,3,0" Background="Transparent">
                <ContentPresenter ContentSource="Header" RecognizesAccessKey="True" HorizontalAlignment="Center"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsSelected" Value="True"><Setter TargetName="Tab" Property="Background" Value="#E4EDE1"/><Setter Property="Foreground" Value="#225E49"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Tab" Property="Background" Value="#EBF1E7"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Tab" Property="BorderBrush" Value="#225E49"/><Setter TargetName="Tab" Property="BorderThickness" Value="1"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
        </ResourceDictionary>
        """;
    private static string Get(JsonElement e,string k,string fallback="")=>e.TryGetProperty(k,out var p)&&p.ValueKind!=JsonValueKind.Null?p.ValueKind==JsonValueKind.String?p.GetString()??fallback:p.ToString():fallback;
    private static JsonElement[] Items(JsonElement e,string k)=>e.TryGetProperty(k,out var p)&&p.ValueKind==JsonValueKind.Array?p.EnumerateArray().ToArray():System.Array.Empty<JsonElement>();
    private static string Join(JsonElement e,string k)=>string.Join("；",Items(e,k).Select(x=>x.ToString()));
    private static string State(string s)=>s switch {
        "QUEUED"=>"等待准入","RUNNING"=>"执行中","PRODUCED"=>"已产出 · 待检查","VERIFIED"=>"已核验","FAILED" or "FAIL"=>"检查失败",
        "BLOCKED"=>"受阻","WAITING_USER"=>"等待用户","UNKNOWN"=>"待核查","PLANNED" or "NOT_RUN"=>"待执行","STALE"=>"已失效 · 需复检",
        "PASS"=>"检查通过","OPEN"=>"问题未解决","RESOLVED"=>"已修复并复检","REPAIRING"=>"修复中","RECHECKING"=>"复检中",
        "NOT_CONFIGURED"=>"未配置","NOT_READY"=>"尚未形成交付候选","PASS_CANDIDATE"=>"待独立验收",
        "INSUFFICIENT_COVERAGE"=>"检查覆盖不足","REQUIRED_CHECK_NOT_DONE"=>"必需检查未完成",
        "RECHECK_EVIDENCE_INSUFFICIENT"=>"复检依据不足","FINAL_EVIDENCE_PREDATES_RECHECK"=>"终检依据需更新",
        "NOT_REQUESTED"=>"未申请验收","WAITING_REVIEW"=>"等待独立验收","ACCEPTED"=>"记录已有外部验收","REJECTED"=>"验收拒收",
        "SUPPORTED"=>"现有工具","SUBSTITUTE"=>"等效替代","UNSUPPORTED"=>"暂不支持","DEVELOPMENT_PENDING"=>"待开发",
        "CRITICAL"=>"严重","MAJOR"=>"重要","MINOR"=>"一般",
        "NOT_APPLICABLE"=>"不适用","CANCELLED"=>"已确认取消","CANCEL_REQUESTED"=>"已请求取消，待确认","SKIPPED"=>"已跳过",_=>s };
    private static string Kind(string s)=>s switch { "PREFLIGHT"=>"预检","CHECK"=>"首检","REPAIR"=>"修复","RECHECK"=>"复检","FINAL"=>"最终检查","ACCEPTANCE"=>"验收",_=>"处理" };
    private static string Phase(JsonElement check)=>Get(check,"phase")=="PROCESS"?"过程首检":Kind(Get(check,"phase"));
    private static string Coverage(JsonElement check)
    {
        if(!check.TryGetProperty("scope",out var scope)||scope.ValueKind!=JsonValueKind.Object)return "尚未记录检查覆盖";
        var coverage=(Get(scope,"mode")=="FULL"?"全量":"抽样")+"：已检查 "+Get(scope,"checked","未知")+" / 总体 "+Get(scope,"population","未知");
        if(Get(scope,"mode")=="FULL"&&Get(scope,"population")=="0") {
            var reason=Get(scope,"emptyRationale").Trim();
            coverage+="\n空范围说明："+(reason.Length>0?reason:"尚未记录适用依据");
        }
        return coverage;
    }
    private static Brush Tint(string s)=>s is "RUNNING" or "REPAIRING" or "RECHECKING" or "VERIFIED" or "PASS" or "RESOLVED" or "ACCEPTED"?Accent:s is "FAILED" or "FAIL" or "REJECTED"?Danger:s is "BLOCKED" or "STALE" or "OPEN" or "WAITING_USER" or "INSUFFICIENT_COVERAGE" or "REQUIRED_CHECK_NOT_DONE" or "RECHECK_EVIDENCE_INSUFFICIENT" or "FINAL_EVIDENCE_PREDATES_RECHECK"?Warn:Muted;
    private ToolProgressEvent? Latest(JsonElement r)=>_latestByStep.GetValueOrDefault(Get(r,"id"));
    private string Effective(JsonElement r)=>_effectiveByStep.GetValueOrDefault(Get(r,"id"),Get(r,"state","UNKNOWN"));
    private void RebuildExecutionView()
    {
        _latestByStep.Clear();_effectiveByStep.Clear();_boundRequests.Clear();
        if(_view is null)return; // A temporarily missing record must not erase already observed reexecution.
        foreach(var row in _rows)_boundRequests.UnionWith(Items(row,"requestIds").Select(x=>x.GetString()!));
        foreach(var id in _executionMemory.Keys.Where(id=>!_boundRequests.Contains(id)).ToArray())_executionMemory.Remove(id);
        // The journal is a bounded window, not a retraction of previously observed effects.
        // Keep one latest observation per declared request until a new task or newer record supersedes it.
        foreach(var ev in _observed) {
            if(ev.RequestId is not { } id||!_boundRequests.Contains(id))continue;
            if(!_executionMemory.TryGetValue(id,out var old)||ev.ObservedAt>old.Event.ObservedAt||ev.ObservedAt==old.Event.ObservedAt&&ev.Sequence>=old.Event.Sequence)_executionMemory[id]=(ev,++_executionOrder);
        }
        foreach(var row in _rows) {
            var ids=Items(row,"requestIds").Select(x=>x.GetString()!).ToArray();
            var candidates=ids.Where(_executionMemory.ContainsKey).Select(id=>_executionMemory[id]).OrderBy(x=>x.Order).Select(x=>x.Event).ToArray();
            var latest=candidates.Length==0?null:WorkflowEventBinding.Latest(row,candidates);var key=Get(row,"id");
            _latestByStep[key]=latest;_effectiveByStep[key]=WorkflowEventBinding.EffectiveState(row,latest);
        }
    }
    private static bool ValidCounter(ToolProgressEvent ev)=>ev.Completed is { } done&&ev.Total is { } total&&double.IsFinite(done)&&double.IsFinite(total)&&total>0&&done>=0&&done<=total;
    private static string CounterText(ToolProgressEvent ev)=>ValidCounter(ev)?$"当前阶段 {ev.Completed:0.##}/{ev.Total:0.##}；非整单完成率":ev.Total is null?"阶段总量未知":"阶段计数无效或总量未知，未用于进度";
    private void SyncPresentationHints()
    {
        _goal.ToolTip=_goal.Text;_active.ToolTip=_active.Text;_intakeSummary.ToolTip=_intakeSummary.Text;_overview.ToolTip=_overview.Text;
        if(_view is null) { _context.ToolTip=_context.Text;_stageText.ToolTip=_stageText.Text;_flowHint.ToolTip=_flowHint.Text; }
    }
    private bool NewExecution()=>_checksStale;
    private void Refresh()
    {
        if(_paused) { _watchState.Text="已暂停观测";_watchState.Foreground=Warn;_freshness.Text="面板已暂停；后端执行不受影响";return; }
        try {
            try { _observed=_read().ToArray();_eventIssue=_journalError(); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { _eventIssue="内部事件读取暂不可用 · "+ex.GetType().Name; }
            var fp=string.IsNullOrWhiteSpace(_taskRecord)||!File.Exists(_taskRecord)?"missing":File.GetLastWriteTimeUtc(_taskRecord).Ticks+":"+new FileInfo(_taskRecord).Length;
            if(fp==_recordFingerprint&&_observed.SequenceEqual(_renderedEvents)&&_eventIssue==_renderedEventIssue) { Freshness();return; }
            var selectedBefore=(_steps.SelectedItem as ListBoxItem)?.Tag as string;
            var selectedEventBefore=selectedBefore is null?null:_latestByStep.GetValueOrDefault(selectedBefore);
            var previousExecutions=_latestByStep.ToArray();
            var recordChanged=fp!=_recordFingerprint;if(recordChanged)LoadRecord(fp);_recordIssue="";RebuildExecutionView();
            var previousChecksStale=_checksStale;_checksStale=_rows.Any(r=>WorkflowEventBinding.ChecksNeedRefresh(r,Latest(r)));
            var checksChanged=previousChecksStale!=_checksStale;
            var executionChanged=previousExecutions.Length!=_latestByStep.Count||previousExecutions.Any(p=>!_latestByStep.TryGetValue(p.Key,out var next)||p.Value!=next);
            if(recordChanged||executionChanged) {
                RenderSteps();
                var selectedAfter=(_steps.SelectedItem as ListBoxItem)?.Tag as string;
                var selectedEventAfter=selectedAfter is null?null:_latestByStep.GetValueOrDefault(selectedAfter);
                if(recordChanged||checksChanged||selectedBefore!=selectedAfter||selectedEventBefore!=selectedEventAfter)RenderDetails();
            }
            // SDK messages do not change the recorded quality contract; preserve reading/focus until its facts change.
            if(recordChanged||checksChanged) { RenderQuality();RenderIntake(); }
            RenderEvents();Freshness();SyncPresentationHints();_renderedEvents=_observed;_renderedEventIssue=_eventIssue;
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) {
            _view=null;_rows=System.Array.Empty<JsonElement>();_checksStale=false;_recordIssue=ex.GetType().Name;_renderGeneration++;_steps.Items.Clear();_stepItems.Clear();_quality.Children.Clear();_intake.Children.Clear();Statistics();
            _latestByStep.Clear();_effectiveByStep.Clear();_boundRequests.Clear();
            EmptyList(true,"任务记录暂不可用","等待有效记录恢复；此前步骤和检查不会继续显示为当前状态。");
            EmptyDetails("等待记录恢复","请让 Agent 核对任务记录。面板会自动重读，不会执行或重试 GIS。");
            _active.Text="当前步骤未知";_intakeSummary.Text="资料和成果位置待核对";
            _stageProgress.Visibility=Visibility.Collapsed;_stageText.Text="任务记录不可用，等待有效记录";
            _goal.Text="任务记录暂不可用";_context.Text="此前状态不能作为当前进度";_overview.Text="读取失败 · "+_recordIssue;_freshness.Text="读取失败";_watchState.Text="记录异常";_watchState.Foreground=Danger;_recordFingerprint="";
            _qualityButton.Content="查看检查说明";_qualityButton.Foreground=Muted;_quality.Children.Add(Text("记录无效，检查状态待恢复",15,Ink));_intake.Children.Add(Text("资料与成果位置待核对",15,Ink));
            RenderEvents();SyncPresentationHints();

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
                var identity=Get(d.RootElement,"taskId");var viewIdentity=Get(v,"taskId");
                if(identity.Length>0&&viewIdentity.Length>0&&identity!=viewIdentity)throw new InvalidOperationException("Conflicting task identity");
                var rows=Items(v,"steps");var ids=new HashSet<string>(StringComparer.Ordinal);var requests=new HashSet<string>(StringComparer.Ordinal);
                if(rows.Length is <1 or >300)throw new InvalidOperationException("Invalid workflow steps");
                foreach(var row in rows) {
                    if(!row.TryGetProperty("id",out var id)||id.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(id.GetString())||!ids.Add(id.GetString()!))throw new InvalidOperationException("Invalid workflow step identity");
                    if(row.TryGetProperty("requestIds",out var bindings)) {
                        if(bindings.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("Invalid request bindings");
                        foreach(var binding in bindings.EnumerateArray())
                            if(binding.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(binding.GetString())||!requests.Add(binding.GetString()!))throw new InvalidOperationException("Ambiguous request binding");
                    }
                }
                if(identity.Length==0)identity=viewIdentity;
                if(identity!=_taskIdentity) { _expanded.Clear();_stepItems.Clear();_executionMemory.Clear();_executionOrder=0;_taskIdentity=identity;_detailSelection="";_detailScroll.ScrollToTop();ResetFilters();_follow=true;UpdateFollowButton(); }
                _view=v.Clone();_rows=Items(_view.Value,"steps");
            }
        }
        _recordFingerprint=fp;
        if(_view is { } view) {
            _goal.Text=Get(view,"goal");var origin=Get(view,"origin");var demo=origin.Contains("SYNTHETIC",StringComparison.OrdinalIgnoreCase)||origin.Contains("DEMO",StringComparison.OrdinalIgnoreCase);
            _goal.ToolTip=_goal.Text;
            _context.Text=(demo?"演示记录 · 非真实 GIS 执行  /  ":"业务流程  /  ")+Get(view,"scenario")+" · v"+Get(view,"revision");
            _context.ToolTip=Get(view,"selectionReason");_context.Foreground=demo?Warn:Muted;
        } else {
            _goal.Text="等待发布任务流程";_context.Text="尚无规范业务记录";_overview.Text="Agent 发布完整步骤后，将在此同步展示。";
        }
    }
    private void RenderSteps()
    {
        var selected=(_steps.SelectedItem as ListBoxItem)?.Tag as string;
        var scroll=VisualChild<ScrollViewer>(_steps);var offset=_resetListScroll?0:scroll?.VerticalOffset??0;_resetListScroll=false;var generation=++_renderGeneration;
        _renderingSteps=true;
        try {
        if(_view is not { } v) {
            _steps.Items.Clear();_stepItems.Clear();
            Statistics();EmptyList(true,"等待任务流程","在对话中确认需求、数据与成果位置，Agent 将同步完整步骤。");
            _flowHint.Text="等待 Agent 发布步骤";_active.Text="当前无已观测的执行中步骤";_stageText.Text="等待实际观测";_stageProgress.Visibility=Visibility.Collapsed;_qualityButton.Content="查看检查与修复";_qualityButton.Foreground=Accent;return;
        }
        var running=_rows.Where(r=>Effective(r)=="RUNNING").ToArray();
        _active.Text=running.Length==0?"当前无已观测的执行中步骤":string.Join("；",running.Select(r=>Get(r,"id")+"  "+Get(r,"title")));
        _active.ToolTip=_active.Text;
        var linear=_rows.Select((r,i)=>i==0?Items(r,"dependsOn").Length==0:Items(r,"dependsOn").Length==1&&Join(r,"dependsOn")==Get(_rows[i-1],"id")).All(x=>x);
        var attention=_rows.Count(r=>Attention(Effective(r)));
        Statistics(_rows.Length,_rows.Count(r=>Effective(r)=="VERIFIED"),running.Length,attention);
        _overview.Text=(NewExecution()?"检查待更新":Get(v,"candidate")=="PASS_CANDIDATE"?"交付候选已形成":State(Get(v,"candidate")))+" · "+State(Get(v,"acceptance"));_overview.ToolTip=_overview.Text;
        var pendingIssues=Items(v,"issues").Count(i=>Get(i,"status")!="RESOLVED");
        var pendingChecks=Items(v,"checks").Count(c=>Get(c,"effectiveState") is not ("PASS" or "NOT_APPLICABLE")||(NewExecution()&&Get(c,"effectiveState")=="PASS"));
        _qualityButton.Content=pendingIssues+pendingChecks==0?"查看检查与修复":$"{pendingIssues} 项问题 / {pendingChecks} 项检查待处理";
        _qualityButton.Foreground=pendingIssues+pendingChecks==0?Accent:Warn;
        var query=_stepSearch.Text.Trim();var visible=_rows.Where(r=>(_attentionOnly.IsChecked!=true||Attention(Effective(r)))&&
            (query.Length==0||(Get(r,"id")+" "+Get(r,"title")+" "+Kind(Get(r,"kind"))+" "+State(Effective(r))+" "+Effective(r)).Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray();
        var visibleIds=visible.Select(r=>Get(r,"id")).ToHashSet(StringComparer.Ordinal);
        _flowHint.Text=$"显示 {visible.Length} / {_rows.Length} 步 · "+(linear?"按顺序":"含依赖分支")+
            (running.Length>0&&!visible.Any(r=>Effective(r)=="RUNNING")?" · 当前执行在筛选外":"");
        _flowHint.ToolTip=_flowHint.Text+"\n仅筛选列表，不改变任务、执行高亮或全任务计数。";EmptyList(visible.Length==0);
        _stageProgress.Visibility=Visibility.Collapsed;
        var stage=running.Select(Latest).Where(e=>e is not null).OrderBy(e=>e!.ObservedAt).LastOrDefault();
        var pendingStep=_rows.FirstOrDefault(r=>Attention(Effective(r)));
        _stageText.Text=running.Length>0?stage?.Message??"等待工具回传阶段进度":pendingStep.ValueKind!=JsonValueKind.Undefined?
            "待关注："+Get(pendingStep,"id")+" · "+Get(pendingStep,"title")+" · "+State(Effective(pendingStep)):"没有新的执行观测；可查看下方检查与成果。";
        _stageText.ToolTip=_stageText.Text;
        if(stage is not null&&ValidCounter(stage)) {
            var completed=stage.Completed!.Value;var total=stage.Total!.Value;
            _stageProgress.Maximum=total;_stageProgress.Value=completed;_stageProgress.Visibility=Visibility.Visible;
            _stageText.Text=$"本阶段 {completed:0.##} / {total:0.##} · 非整单完成率";_stageText.ToolTip=stage.Message;
        }
        var currentIds=_rows.Select(r=>Get(r,"id")).ToHashSet(StringComparer.Ordinal);
        foreach(var id in _stepItems.Keys.Where(id=>!currentIds.Contains(id)).ToArray())_stepItems.Remove(id);
        var desired=new List<ListBoxItem>();
        for(var index=0;index<_rows.Length;index++) {
            var row=_rows[index];var id=Get(row,"id");if(!visibleIds.Contains(id))continue;var state=Effective(row);
            var note=state is "RUNNING" or "FAILED" or "BLOCKED" or "STALE" or "WAITING_USER"?Latest(row)?.Message??Get(row,"summary"):"";
            var appearance=new StepAppearance(Get(row,"title"),Get(row,"kind"),state,note,index);
            if(_stepItems.TryGetValue(id,out var cached)&&cached.Appearance==appearance) { desired.Add(cached.Item);continue; }
            var card=new Grid { Margin=new Thickness(7,9,7,9) };
            card.ColumnDefinitions.Add(new() { Width=new GridLength(25) });card.ColumnDefinitions.Add(new());card.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
            var number=Text(state=="VERIFIED"?"✓":(index+1).ToString("D2"),12,state=="RUNNING"?Brushes.White:Tint(state));number.HorizontalAlignment=System.Windows.HorizontalAlignment.Center;
            var dot=new Border { Child=number,Width=23,Height=23,CornerRadius=new CornerRadius(12),Background=state=="RUNNING"?Accent:Paint("#E7EEE4"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0) };card.Children.Add(dot);
            var copy=new StackPanel { Margin=new Thickness(7,0,6,0),VerticalAlignment=VerticalAlignment.Center };
            var label=OneLine(Get(row,"title"),13,state=="RUNNING"?Accent:Ink);if(state=="RUNNING")label.FontWeight=FontWeights.SemiBold;copy.Children.Add(label);
            var hint=Get(row,"id")+" · "+Kind(Get(row,"kind"));
            if(note.Length>0)hint+=" · "+note;
            copy.Children.Add(OneLine(hint,10,Muted));Grid.SetColumn(copy,1);card.Children.Add(copy);
            var badgeText=Text(state=="RUNNING"?"执行中":State(state),10,Tint(state));
            var badge=new Border { Child=badgeText,Padding=new Thickness(4,0,4,0),CornerRadius=new CornerRadius(5),Background=state=="RUNNING"?Paint("#DAE9D7"):Brushes.Transparent,VerticalAlignment=VerticalAlignment.Center };Grid.SetColumn(badge,2);card.Children.Add(badge);
            // Execution highlight belongs to the content, independent of selection/manual inspection.
            var executionCard=new Border { Child=card,CornerRadius=new CornerRadius(7),
                Background=state=="RUNNING"?Paint("#E3EFDF"):Brushes.Transparent,
                BorderBrush=state=="RUNNING"?Accent:Brushes.Transparent,BorderThickness=new Thickness(state=="RUNNING"?3:0,0,0,0) };
            var item=new ListBoxItem { Tag=Get(row,"id"),Content=executionCard,Background=Brushes.Transparent,BorderThickness=new Thickness(0) };
            // Reused virtualized containers must shrink with the visible viewport, not retain an old extent width.
            item.SetBinding(FrameworkElement.WidthProperty,new System.Windows.Data.Binding(nameof(ScrollViewer.ViewportWidth)) {
                RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(ScrollViewer),1) });
            AutomationProperties.SetAutomationId(item,"step-"+id);AutomationProperties.SetName(item,id+" "+Get(row,"title")+" "+State(state));_stepItems[id]=(appearance,item);desired.Add(item);
        }
        var keep=desired.ToHashSet();
        for(var i=_steps.Items.Count-1;i>=0;i--)if(!keep.Contains((ListBoxItem)_steps.Items[i]))_steps.Items.RemoveAt(i);
        for(var i=0;i<desired.Count;i++) {
            if(i<_steps.Items.Count&&ReferenceEquals(_steps.Items[i],desired[i]))continue;
            var oldIndex=_steps.Items.IndexOf(desired[i]);if(oldIndex>=0)_steps.Items.RemoveAt(oldIndex);
            _steps.Items.Insert(i,desired[i]);
        }
        var focus=_rows.FirstOrDefault(r=>Effective(r)=="RUNNING");
        if(focus.ValueKind==JsonValueKind.Undefined)focus=_rows.Where(r=>Get(r,"observedAt").Length>0).OrderBy(r=>DateTimeOffset.TryParse(Get(r,"observedAt"),out var at)?at:DateTimeOffset.MinValue).LastOrDefault();
        var focusId=focus.ValueKind==JsonValueKind.Undefined?null:Get(focus,"id");
        var selectedItem=(_follow?_steps.Items.Cast<ListBoxItem>().FirstOrDefault(i=>(string)i.Tag==focusId):_steps.Items.Cast<ListBoxItem>().FirstOrDefault(i=>(string)i.Tag==selected))??_steps.Items.Cast<ListBoxItem>().FirstOrDefault();
        _steps.SelectedItem=selectedItem;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=> {
            if(generation!=_renderGeneration||!IsLoaded)return;
            if(_follow&&_steps.SelectedItem is { } current)_steps.ScrollIntoView(current);
            else VisualChild<ScrollViewer>(_steps)?.ScrollToVerticalOffset(offset);
        }));
        } finally { _renderingSteps=false; }
    }
    private void RenderDetails()
    {
        _details.Children.Clear();
        if(_view is not { } v) { EmptyDetails("先确认任务，再开始处理","01  在对话中说明分析需求。\n\n02  提供数据位置，Agent 检查缺失资料和可用工具。\n\n03  确认成果目录后，完整处理、检查与验收步骤将在这里同步。");return; }
        var selected=(_steps.SelectedItem as ListBoxItem)?.Tag as string;
        var r=_rows.FirstOrDefault(r=>Get(r,"id")==selected);if(r.ValueKind==JsonValueKind.Undefined) { EmptyDetails("选择一个步骤","清空筛选或调整关键词后查看步骤；当前执行仍在顶部单独显示。");return; }
        if(_detailSelection!=selected) { _detailSelection=selected??"";_detailScroll.ScrollToTop(); }
        _details.Children.Add(Text(Get(r,"id")+" / "+Kind(Get(r,"kind")),11,Muted));
        _details.Children.Add(Text(Get(r,"title"),19,Ink));_details.Children.Add(Text(State(Effective(r)),12,Tint(Effective(r))));
        Field(_details,"处理结果",Get(r,"summary"));Field(_details,"检查重点",Join(r,"checkFocus"));
        var technical=new StackPanel();
        foreach(var p in new[] {("方法与参数",Get(r,"method")),("依赖步骤",Join(r,"dependsOn")),("输入",Join(r,"inputs")),("计划输出 · 尚不代表已生成",Join(r,"outputs")),("证据",Join(r,"evidenceRefs"))})Field(technical,p.Item1,p.Item2);
        _details.Children.Add(Fold("方法、输入输出与证据",technical,selected+"-technical"));
        var reqs=Items(r,"requirementIds").Select(x=>x.GetString()).ToHashSet();
        var criteria=new StackPanel();foreach(var req in Items(v,"requirements").Where(x=>reqs.Contains(Get(x,"id")))) { Field(criteria,Get(req,"title"),Get(req,"criterion")+"\n依据："+Get(req,"source")); }
        if(criteria.Children.Count>0)_details.Children.Add(Fold("适用判据与来源",criteria,selected+"-criteria"));
        foreach(var c in Items(v,"checks").Where(c=>reqs.Contains(Get(c,"requirementId"))))RenderCheck(_details,c);
        if(Latest(r) is { } ev)Field(_details,"最新内部观测",ev.Message+"\n"+CounterText(ev));
    }
    private void RenderCheck(StackPanel target,JsonElement c)
    {
        var s=NewExecution()&&Get(c,"effectiveState")=="PASS"?"STALE":Get(c,"effectiveState");var detail=new StackPanel();
        Field(detail,"结果",Get(c,"result",Join(c,"evidenceSummary")));Field(detail,"覆盖",Coverage(c));Field(detail,"判据",Get(c,"criterion"));Field(detail,"依据",Get(c,"criterionSource"));Field(detail,"证据",Join(c,"evidenceRefs"));
        var fold=Fold(Phase(c)+" · "+Get(c,"title")+" · "+State(s),detail,"check-"+Get(c,"id"),s is "FAIL" or "STALE" or "UNKNOWN");fold.Foreground=Tint(s);target.Children.Add(fold);
    }
    private void RenderQuality()
    {
        _quality.Children.Clear();if(_view is not { } v) { _quality.Children.Add(Text("尚无可核验的任务流程",16,Ink));_quality.Children.Add(Text("完整流程发布后，将展示预检、过程检查、修复、复检及最终验收。",12,Muted));return; }_quality.Children.Add(Text("质量检查",18,Ink));
        _quality.Children.Add(Text(NewExecution()?"发现新执行，旧检查待更新":"交付："+State(Get(v,"candidate"))+"\n验收："+State(Get(v,"acceptance")),12,Accent));
        if(v.TryGetProperty("gates",out var gates)) {
            var phaseGrid=new Grid { Margin=new Thickness(0,12,0,12) };for(var i=0;i<4;i++)phaseGrid.ColumnDefinitions.Add(new());
            var phases=new[] {("PREFLIGHT","预检"),("PROCESS","过程"),("RECHECK","复检"),("FINAL","终检")};
            for(var i=0;i<phases.Length;i++) {
                var (key,label)=phases[i];var cell=new StackPanel();cell.Children.Add(Text(label,11,Muted));
                var state=gates.TryGetProperty(key,out var gate)?Get(gate,"state"):"NOT_RUN";
                if(NewExecution()&&state=="PASS")state="STALE";
                cell.Children.Add(Text(State(state),11,Tint(state)));var border=Card(cell,8);border.Margin=new Thickness(0,0,i<3?5:0,0);Grid.SetColumn(border,i);phaseGrid.Children.Add(border);
            }
            _quality.Children.Add(phaseGrid);
        }
        foreach(var issue in Items(v,"issues")) {
            var issuePanel=new StackPanel();issuePanel.Children.Add(Text(Get(issue,"description"),12,Ink));
            if(issue.TryGetProperty("repair",out var repair))Field(issuePanel,"修复与复检",State(Get(repair,"state"))+"\n"+Get(repair,"action")+"\n受影响步骤："+Join(repair,"affectedStepIds")+"\n复检："+Get(repair,"recheckResult"));
            Field(issuePanel,"修复/复检证据",Join(issue,"resolutionEvidenceRefs"));
            var issueFold=Fold(Get(issue,"id")+" · "+State(Get(issue,"status"))+" · "+State(Get(issue,"severity")),issuePanel,"issue-"+Get(issue,"id"),Get(issue,"status")!="RESOLVED");issueFold.Foreground=Tint(Get(issue,"status"));_quality.Children.Add(issueFold);
        }
        if(Items(v,"issues").Length==0)_quality.Children.Add(Text("记录未列问题；不等于全部检查通过。",11,Muted));
        foreach(var c in Items(v,"checks"))RenderCheck(_quality,c);
        var limits=new StackPanel();foreach(var s in Items(v,"standards"))limits.Children.Add(Text(s.ValueKind==JsonValueKind.Object?Get(s,"source")+" · "+Get(s,"scope"):s.ToString(),11,Muted));foreach(var s in Items(v,"constraints"))limits.Children.Add(Text(s.ToString(),11,Warn));
        _quality.Children.Add(Fold("依据与适用限制",limits,"quality-limits"));
        var history=new StackPanel();foreach(var old in Items(v,"qualityHistory"))history.Children.Add(Text("历史检查："+old,11,Muted));
        foreach(var change in Items(v,"changes"))history.Children.Add(Text("方案变更："+change,11,Warn));
        _quality.Children.Add(Fold($"检查历史 {Items(v,"qualityHistory").Length} · 方案变更 {Items(v,"changes").Length}",history,"quality-history"));
    }
    private void RenderIntake()
    {
        _intake.Children.Clear();_artifactPathSerial=0;if(_view is not { } v) { _intakeSummary.Text="资料和成果位置尚未记录";_intake.Children.Add(Text("请在对话中确认需求、数据与成果目录",16,Ink));return; }
        _intake.Children.Add(Text("资料与交付",18,Ink));
        var artifacts=Items(v,"artifacts");var produced=artifacts.Where(a=>Get(a,"state") is "PRODUCED" or "VERIFIED").ToArray();
        _intake.Children.Add(Text("实际成果 · "+produced.Length+" 项",13,Accent));
        _intake.Children.Add(Text("按任务记录展示；本面板不独立证明文件存在或成果合格。",10,Muted));
        if(NewExecution())_intake.Children.Add(Text("发现新执行，旧成果核验待复核；请以新检查记录确认。",11,Warn));
        if(produced.Length==0)_intake.Children.Add(Text("尚未记录实际成果，计划路径不作为交付。",12,Muted));
        foreach(var artifact in produced) {
            var artifactState=NewExecution()&&Get(artifact,"state")=="VERIFIED"?"STALE":Get(artifact,"state");
            _intake.Children.Add(Text(Get(artifact,"label")+" · "+State(artifactState),12,Tint(artifactState)));
            ArtifactPath(_intake,Get(artifact,"path","尚无路径"),Get(artifact,"label"));
        }
        var pending=artifacts.Where(a=>Get(a,"state") is not ("PRODUCED" or "VERIFIED")).ToArray();
        if(pending.Length>0) {
            var paths=new StackPanel();foreach(var artifact in pending) {
                paths.Children.Add(Text(Get(artifact,"label")+" · "+(Get(artifact,"state")=="PLANNED"?"计划输出，尚未生成":State(Get(artifact,"state"))),11,Warn));
                ArtifactPath(paths,Get(artifact,"path","尚无路径"),Get(artifact,"label"));
            }
            _intake.Children.Add(Fold("计划与待核对路径 · "+pending.Length+" 项",paths,"intake-pending-paths"));
        }
        if(v.TryGetProperty("intake",out var intake)&&Get(intake,"status")!="LEGACY_UNCONFIRMED") {
            var root=intake.TryGetProperty("outputRoot",out var output)?Get(output,"value"):"待提供";
            _intakeSummary.Text=(Get(intake,"status")=="READY"?"资料已确认":"资料待补充 / 确认")+"    /    成果目录  "+root;_intakeSummary.ToolTip=_intakeSummary.Text;
            _intakeSummary.Foreground=Get(intake,"status")=="READY"?Muted:Warn;
            foreach(var entry in new[] {("requirements","用户需求"),("inputs","数据位置"),("outputRoot","成果保存位置")}) {
                if(!intake.TryGetProperty(entry.Item1,out var row))continue;
                _intake.Children.Add(Text(entry.Item2+" · "+(Get(row,"status")=="CONFIRMED"?"已确认":"待确认"),13,Accent));
                _intake.Children.Add(Text(entry.Item1=="inputs"?Join(row,"value"):Get(row,"value","待提供"),12,Ink));
            }
            foreach(var q in Items(intake,"questions"))_intake.Children.Add(Text("请在对话中答复："+q.ToString(),12,Warn));
            foreach(var gap in Items(intake,"dataNeeds")) {
                _intake.Children.Add(Text("补充资料："+Get(gap,"label")+" · "+Get(gap,"state"),14,Warn));
                _intake.Children.Add(Text("用途："+Get(gap,"reason")+"\n补充建议："+Get(gap,"recommendation")+"\n影响步骤："+Join(gap,"stepIds"),12,Muted));
            }
            var toolPlan=new StackPanel();foreach(var choice in Items(intake,"toolChoices")) {
                toolPlan.Children.Add(Text("工具方案 "+Get(choice,"stepId")+" · "+State(Get(choice,"status")),12,Accent));
                toolPlan.Children.Add(Text(Join(choice,"tools")+"\n"+Get(choice,"reason"),12,Ink));
                foreach(var field in new[] {("equivalence","等效条件"),("limits","差异与限制"),("alternativesConsidered","已评估替代方案")})
                    if(Get(choice,field.Item1).Length>0)toolPlan.Children.Add(Text(field.Item2+"："+Get(choice,field.Item1),12,Muted));
            }
            if(toolPlan.Children.Count>0)_intake.Children.Add(Fold("工具选择与替代评估",toolPlan,"intake-tools"));
        } else { _intakeSummary.Foreground=Warn;_intakeSummary.Text="旧记录未确认本任务的需求、数据和成果目录";_intake.Children.Add(Text(_intakeSummary.Text,12,Warn)); }
        _intake.Children.Add(Text("修改需求、补充资料或确认开发，请在聊天中答复。",11,Muted));
    }
    private bool IsTaskEvent(ToolProgressEvent ev)=>_view is not null&&ev.RequestId is not null&&_boundRequests.Contains(ev.RequestId);
    private ToolProgressEvent? LastTaskEvent()=>_latestByStep.Values.Where(e=>e is not null).OrderBy(e=>e!.ObservedAt).ThenBy(e=>e!.Sequence).LastOrDefault();
    private static string EventKey(ToolProgressEvent ev)=>"event-"+ev.InvocationId+"-"+ev.Sequence+"-"+ev.ObservedAt.UtcDateTime.Ticks;
    private static string MessagePreview(string? message)
    {
        if(string.IsNullOrEmpty(message))return "";
        var length=Math.Min(message.Length,180);
        if(length<message.Length&&char.IsHighSurrogate(message[length-1]))length--;
        var text=new string(message.Take(length).Select(c=>char.IsWhiteSpace(c)?' ':c).ToArray()).Trim();
        return text+(length<message.Length?"…（展开事件查看全文）":"");
    }
    private void RenderEvents()
    {
        var visible=_observed.TakeLast(80).Reverse().ToArray();var keys=visible.Select(EventKey).ToHashSet(StringComparer.Ordinal);
        foreach(var key in _expanded.Keys.Where(k=>k.StartsWith("event-",StringComparison.Ordinal)&&!keys.Contains(k)).ToArray())_expanded.Remove(key);
        _events.Children.Clear();var bound=LastTaskEvent();var last=_view is null?_observed.LastOrDefault():bound;
        if(_eventIssue is not null)_events.Children.Add(Text("事件读取提示："+_eventIssue+"。仅保留可读记录与最后有效观测；不重试 GIS。",11,Warn));
        _internal.Text=last is null?(_view is null?"尚无内部事件":"本任务尚无绑定内部事件；进程日志不能代作本任务运行证明。"):
            (_view is null?"进程内部观测 · 未绑定业务流程":"本任务最新内部观测")+" · "+last.ObservedAt.ToLocalTime().ToString("HH:mm:ss")+"\n"+last.Tool+" · "+State(last.State)+"\n"+MessagePreview(last.Message)+"\n"+CounterText(last);
        if(bound is not null&&!_observed.Contains(bound))_events.Children.Add(Text("当前日志中未见该观测，保留本窗口最后有效状态；不代表持续执行或核验通过。关闭重开后以业务记录和可读日志为准。",11,Warn));
        if(_observed.Count>0)_events.Children.Add(Text("进程事件 · 本任务 / 其他请求（含未关联请求）分别标注",10,Muted));
        foreach(var e in visible) {
            var details=Text(e.Message+"\n"+CounterText(e)+"\n来源："+e.Source+"\n请求："+e.RequestId,11,Muted);
            _events.Children.Add(Fold((IsTaskEvent(e)?"本任务":"其他请求")+" · "+e.ObservedAt.ToLocalTime().ToString("HH:mm:ss.fff")+" · "+State(e.State)+" · "+e.Tool,details,EventKey(e)));
        }
    }
    private void ArtifactPath(StackPanel target,string path,string label)
    {
        var box=new TextBox { Text=path,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Background=Paint("#EEF3EA"),BorderThickness=new Thickness(0),Padding=new Thickness(8),Foreground=Ink,FontSize=11,MaxHeight=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        AutomationProperties.SetName(box,"成果路径 · "+label);AutomationProperties.SetAutomationId(box,"ArtifactPath-"+_artifactPathSerial++);
        target.Children.Add(box);
        var copy=new Button { Content="复制路径",FontSize=10,Padding=new Thickness(8,4,8,4),HorizontalAlignment=System.Windows.HorizontalAlignment.Left,Margin=new Thickness(0,4,0,10),ToolTip=path };
        AutomationProperties.SetName(copy,"复制路径 · "+label);
        copy.Click+=(_,_)=> { try { System.Windows.Clipboard.SetText(path);copy.Content="已复制路径"; }catch(System.Runtime.InteropServices.ExternalException) { copy.Content="剪贴板忙，请重试"; } };
        copy.IsEnabled=path!="尚无路径"&&path.Length>0;target.Children.Add(copy);
    }
    private void Freshness()
    {
        if(_view is not { } v) { _watchState.Text=_eventIssue is null?"等待流程":"事件读取异常";_watchState.Foreground=_eventIssue is null?Muted:Warn;_freshness.Text=_eventIssue is null?"没有业务任务记录；内部日志可单独查看。":"内部事件读取异常；等待业务流程。";_freshness.ToolTip=(_eventIssue??_freshness.Text)+"\n"+_sourceLabel+"\n业务记录："+(_taskRecord??"未配置");return; }
        var bound=LastTaskEvent();var raw=Get(v,"observedAt");var hasStamp=DateTimeOffset.TryParse(raw,out var stamp);
        var age=hasStamp?(DateTimeOffset.UtcNow-stamp).TotalSeconds:double.NaN;
        var sdkAge=bound is null?double.NaN:(DateTimeOffset.UtcNow-bound.ObservedAt).TotalSeconds;var sdkFresh=sdkAge>= -30&&sdkAge<=15;
        var journalError=_eventIssue;var clockAhead=age< -30||sdkAge< -30;
        _watchState.Text=journalError is not null?"事件读取异常":clockAhead?"记录时钟待核对":age>= -30&&age<=15?"记录已同步":sdkFresh?"内部进度已更新":double.IsNaN(age)?"尚无观测时间":"暂无新记录";
        _watchState.Foreground=journalError is not null||clockAhead?Warn:Muted;
        _freshness.Text="流程 "+(hasStamp?stamp.ToLocalTime().ToString("HH:mm:ss"):"无时间")+" · SDK "+(bound?.ObservedAt.ToLocalTime().ToString("HH:mm:ss")??"无绑定")+(clockAhead?" · 时钟待核对":"")+(journalError is not null?" · 事件异常":"");
        _freshness.ToolTip="流程记录观测："+raw+"\n本任务 SDK："+(bound?.ObservedAt.ToString("o")??"无绑定事件")+"\n"+_sourceLabel+"\n业务记录："+(_taskRecord??"未配置")+"\n"+(journalError??"仅表示观测的新鲜度，不推断任务仍在执行或已经通过验收。")+"\n已观测状态保留在本窗口；重开后以业务记录及可读日志为准。";
    }
}
