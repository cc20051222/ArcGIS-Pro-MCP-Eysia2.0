using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcGISProMCP.Compatibility.Controls;
using ArcGISProMCP.Core.Progress;

namespace ArcGISProMCP.ProgressMonitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? Value(string key) { var i = Array.IndexOf(args, key); return i >= 0 && i+1 < args.Length ? args[i+1] : null; }
        var directory = Value("--directory");
        if (string.IsNullOrWhiteSpace(directory) || !Path.GetFullPath(directory).StartsWith(@"D:\", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show("请用 --directory 指定 D 盘已授权的进度记录目录。面板不会执行 GIS 或启动服务。"); return; }
        var app = new Application();
        Func<DateTime>? sceneryClock=null;
        // Explicit UI test hook: the visible scene label says 时间演示. Never changes the OS clock or task timestamps.
        if(Value("--scenery-clock-fixture") is { } fixture)
        {
            if(!Path.GetFullPath(fixture).StartsWith(@"D:\",StringComparison.OrdinalIgnoreCase)||!File.Exists(fixture))
            { MessageBox.Show("时间演示夹具必须是 D 盘已有文件。");return; }
            sceneryClock=()=> {
                try { return DateTime.ParseExact(File.ReadAllText(fixture).Trim(),"yyyy-MM-dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture); }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or FormatException) { return DateTime.Now; }
            };
        }
        ProgressJournalReadResult? journal=null;
        var window = new ProgressPanelWindow(() => { journal=ProgressJournalReader.ReadWithStatus(directory);return journal.Events; },
            journalError:()=>journal?.Error,taskRecord: Value("--task-record"),
            sourceLabel: "本地事件记录：" + directory,sceneryClock:sceneryClock);
        if (Value("--capture") is { } capture && Path.GetFullPath(capture).StartsWith(@"D:\", StringComparison.OrdinalIgnoreCase))
        {
            var frames = int.TryParse(Value("--capture-frames"), out var requested) ? Math.Clamp(requested, 1, 3) : 1;
            var frame = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) =>
            {
                var target = frames == 1 ? capture : Path.Combine(Path.GetDirectoryName(capture)!, Path.GetFileNameWithoutExtension(capture) + "-" + frame.ToString("D2") + ".png");
                try
                {
                    window.UpdateLayout();
                    // Capture the live WPF client visual, excluding native chrome and overlays from other programs.
                    var visual=(FrameworkElement)window.Content;
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
                    using var file = new FileStream(target, FileMode.CreateNew); encoder.Save(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { window.Title = "任务面板 · 截帧未保存（不覆盖已有文件）"; }
                if (++frame >= frames) timer.Stop();
            };
            window.Loaded += (_, _) => timer.Start();
        }
        app.Run(window);
    }
}
