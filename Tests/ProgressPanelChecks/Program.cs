using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Progress;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Server;
using ArcGISProMCP.Server.Transport;
using ArcGISProMCP.TestSupport;

namespace ArcGISProMCP.ProgressPanelChecks;

internal static class Program
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../.."));
    private static readonly string Run = Path.Combine(Environment.GetEnvironmentVariable("PROGRESS_CHECK_ROOT")
        ?? @"D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel", "checks-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
    private static readonly List<object> Results = new();
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Request(int id=1, object? token=null, string name="progress_probe")
        => JsonSerializer.Serialize(new { jsonrpc="2.0", id, method="tools/call", @params=new
        { name, arguments=new { }, _meta=new { progressToken=token } } });
    private static McpServer Server(IMCPTool tool, ToolProgressHub hub, HttpMcpTransport? transport=null, int timeout=5000)
    {
        var registry=new MCPToolRegistry(); registry.Register(tool); var settings=new MCPSettings { RequestTimeoutMs=timeout };
        var router=new MCPToolRouter(registry,new FakeArcGISHost(),settings,NullLogger.Instance);
        return new McpServer(transport ?? new HttpMcpTransport("127.0.0.1",16524,"/mcp",NullLogger.Instance),
            registry,router,settings,NullLogger.Instance,progress:hub);
    }
    private static async Task Check(string name, Func<Task> action)
    {
        try { await action(); Results.Add(new { name, result="PASS" }); Console.WriteLine("PASS "+name); }
        catch(Exception ex) { Results.Add(new { name,result="FAIL",error=ex.ToString() }); Console.WriteLine("FAIL "+name+": "+ex.Message); }
    }
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }

    public static async Task<int> Main(string[] args)
    {
        Directory.CreateDirectory(Run);
        if (args.Contains("--demo")) { await Demo(); return 0; }
        await Check("workflow binds exact request IDs, never a tool-name match",()=>Sync(()=>
        {
            using var doc=JsonDocument.Parse("{\"state\":\"PLANNED\",\"requestIds\":[\"taskA-step3\"]}");
            var now=DateTimeOffset.UtcNow;
            var unrelated=new ToolProgressEvent(1,"other","taskB-step3","buffer","gp","RUNNING",1,100,"other","SDK",now);
            Require(WorkflowEventBinding.Latest(doc.RootElement,new[]{unrelated}) is null,"unrelated workflow attached");
            Require(WorkflowEventBinding.EffectiveState(doc.RootElement,unrelated)=="PLANNED","unbound event changed state");
            var bound=unrelated with { RequestId="taskA-step3" };
            Require(WorkflowEventBinding.Latest(doc.RootElement,new[]{unrelated,bound})==bound,"exact request not attached");
        }));
        await Check("workflow tool success cannot certify a step and newer execution invalidates old check",()=>Sync(()=>
        {
            using var doc=JsonDocument.Parse("{\"state\":\"VERIFIED\",\"requestIds\":[\"r\"],\"observedAt\":\"2026-10-18T10:00:00Z\"}");
            var later=new ToolProgressEvent(1,"a","r","quality","request","PRODUCED",null,null,"returned","mcp",DateTimeOffset.Parse("2026-10-18T10:01:00Z"));
            Require(WorkflowEventBinding.EffectiveState(doc.RootElement,later)=="PRODUCED","false verification");
            Require(WorkflowEventBinding.ChecksNeedRefresh(doc.RootElement,later),"old quality reused");
            Require(WorkflowEventBinding.EffectiveState(doc.RootElement,later with { ObservedAt=DateTimeOffset.Parse("2026-10-18T09:59:00Z") })=="VERIFIED","older event replaced new observation");
        }));
        await Check("initialize distinguishes the progress-enabled backend", async () =>
        {
            var response = await Server(new Probe(), new ToolProgressHub()).HandleRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}");
            using var doc = JsonDocument.Parse(response!);
            var feature = doc.RootElement.GetProperty("result").GetProperty("capabilities")
                .GetProperty("experimental").GetProperty("arcgispro-mcp/progress");
            Require(feature.GetProperty("lifecycleEvents").GetBoolean() && !feature.GetProperty("globalCompletionPercentage").GetBoolean(), "capabilities misrepresented");
        });
        await Check("request scopes isolate concurrent callbacks",async () =>
        {
            var hub=new ToolProgressHub();
            await Task.WhenAll(Enumerable.Range(0,8).Select(async i =>
            {
                using var scope=ToolProgress.Begin(hub,i.ToString(),"tool_"+i);
                await Task.Yield(); ToolProgress.Report("inside","RUNNING","row",i,8);
            }));
            var rows=hub.Snapshot();Require(rows.Count==8 && rows.All(e=>e.Tool=="tool_"+e.RequestId),"crossed requests");
        });
        await Check("late callback ignored after scope closes",()=>Sync(() =>
        {
            var hub=new ToolProgressHub(); var scope=ToolProgress.Begin(hub,"one","test");scope.Dispose();
            scope.Report("late","RUNNING","late");Require(hub.Snapshot().Count==0,"late event accepted");
        }));
        await Check("invalid counters stay unknown, never fake percent",()=>Sync(() =>
        {
            var hub=new ToolProgressHub();using var scope=ToolProgress.Begin(hub,"x","test");
            scope.Report("one","RUNNING","invalid",101,100);scope.Report("two","RUNNING","invalid",double.NaN,0);
            Require(hub.Snapshot().All(e=>e.Completed is null && e.Total is null),"invalid numeric progress survived");
        }));
        await Check("bounded memory feed",()=>Sync(() =>
        {
            var hub=new ToolProgressHub();using var scope=ToolProgress.Begin(hub,"x","test");
            for(var i=0;i<2500;i++)scope.Report("row","RUNNING","row");
            Require(hub.Snapshot().Count==ToolProgressHub.Capacity,"unbounded feed");
        }));
        await Check("C drive rejected before directory creation",()=>Sync(() =>
        {
            var rejected=false;try{new ToolProgressHub().ConfigureJournal(@"C:\must-not-be-written\progress");}catch(ArgumentException){rejected=true;}
            Require(rejected,"C accepted");
        }));
        await Check("journal tolerates partial and corrupt records",()=>Sync(() =>
        {
            var dir=Path.Combine(Run,"partial");Directory.CreateDirectory(dir);
            var p=Path.Combine(dir,"progress-partial.jsonl");
            var e=new ToolProgressEvent(1,"scope","1","probe","inside","RUNNING",1,3,"中文","fixture",DateTimeOffset.UtcNow);
            var json=JsonSerializer.Serialize(e);File.WriteAllText(p,"broken\n"+json[..10]);
            Require(ProgressJournalReader.Read(dir).Count==0,"partial line accepted");
            File.AppendAllText(p,json[10..]+"\n");Require(ProgressJournalReader.Read(dir).Single().Message=="中文","completed line lost");
        }));
        await Check("observer failure cannot fail or repeat a tool",async () =>
        {
            var tool=new Probe();var server=Server(tool,new ToolProgressHub());
            var result=await server.HandleStreamingRequestAsync(Request(token:"bad-observer"),_=>throw new IOException());
            Require(tool.Calls==1 && result!.Contains("\"isError\":false"),"observer changed effects");
        });
        await Check("token-correlated monotonic events and honest completion",async () =>
        {
            var hub=new ToolProgressHub();var messages=new List<string>();var server=Server(new Probe(),hub);
            var result=await server.HandleStreamingRequestAsync(Request(token:0),messages.Add);
            long previous=0;
            foreach(var message in messages)
            {
                using var doc=JsonDocument.Parse(message);var p=doc.RootElement.GetProperty("params");
                Require(p.GetProperty("progressToken").GetInt32()==0,"numeric token lost");
                var value=p.GetProperty("progress").GetInt64();Require(value>previous,"nonmonotonic sequence");previous=value;
                Require(!p.TryGetProperty("total",out _),"manufactured total");
            }
            Require(messages.Count>=5 && hub.Snapshot().Last().State=="PRODUCED","missing lifetime events");
            Require(hub.Snapshot().All(e=>e.State!="VERIFIED"),"tool success became quality acceptance");
            Require(result!.Contains("\"isError\":false"),"final response changed");
        });
        await Check("no token keeps JSON behavior and local observations",async () =>
        {
            var hub=new ToolProgressHub();var messages=new List<string>();
            var result=await Server(new Probe(),hub).HandleStreamingRequestAsync(Request(),messages.Add);
            Require(messages.Count==0 && hub.Snapshot().Count>0 && result!.Contains("\"isError\":false"),"JSON fallback changed");
        });
        await Check("timeout is unknown effects, never verified",async () =>
        {
            var hub=new ToolProgressHub();var result=await Server(new Probe { WaitForever=true },hub,timeout:100).HandleRequestAsync(Request());
            Require(hub.Snapshot().Last().State=="UNKNOWN" && result!.Contains("REQUEST_TIMEOUT"),"timeout misreported");
        });
        await Check("real HTTP flush precedes tool completion and final response terminates stream",async () =>
        {
            var probe=new Probe { Gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
            var hub=new ToolProgressHub();var transport=new HttpMcpTransport("127.0.0.1",16524,"/mcp",NullLogger.Instance);
            var server=Server(probe,hub,transport);using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(10) };
            await server.StartAsync();
            try
            {
                using var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:16524/mcp");
                req.Headers.TryAddWithoutValidation("Accept","application/json, text/event-stream");
                req.Content=new StringContent(Request(token:"stream-1"),Encoding.UTF8,"application/json");
                using var response=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead);
                Require(response.Content.Headers.ContentType!.MediaType=="text/event-stream","not SSE");
                using var reader=new StreamReader(await response.Content.ReadAsStreamAsync());
                string? first;do{first=await reader.ReadLineAsync();}while(first is not null && !first.StartsWith("data: "));
                Require(first is not null && first.Contains("notifications/progress"),"no early progress");
                Require(!probe.Completed,"stream buffered until completion");probe.Gate.TrySetResult();
                var tail=await reader.ReadToEndAsync();var payloads=tail.Split('\n').Where(x=>x.StartsWith("data: ")).ToArray();
                Require(payloads.Length>0 && payloads.Last().Contains("\"result\":"),"missing final result");
                var eventCount=hub.Snapshot().Count;await Task.Delay(40);Require(hub.Snapshot().Count==eventCount,"events after completion");
            }
            finally{probe.Gate.TrySetResult();await server.StopAsync();}
        });
        await Check("foreign Origin blocked before dispatch",async () =>
        {
            var probe=new Probe();var transport=new HttpMcpTransport("127.0.0.1",16524,"/mcp",NullLogger.Instance);
            var server=Server(probe,new ToolProgressHub(),transport);await server.StartAsync();
            try
            {
                using var client=new HttpClient();using var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:16524/mcp");
                req.Headers.TryAddWithoutValidation("Origin","https://foreign.example");req.Content=new StringContent(Request(),Encoding.UTF8,"application/json");
                using var response=await client.SendAsync(req);Require(response.StatusCode==HttpStatusCode.Forbidden && probe.Calls==0,"foreign Origin dispatched");
            }
            finally{await server.StopAsync();}
        });
        await Check("SSE heartbeat keeps a quiet stream alive without inventing work progress",async () =>
        {
            var probe=new Probe { Gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
            var hub=new ToolProgressHub();var transport=new HttpMcpTransport("127.0.0.1",16524,"/mcp",NullLogger.Instance);
            var server=Server(probe,hub,transport,timeout:20000);await server.StartAsync();
            try
            {
                using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(20)};
                using var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:16524/mcp");
                req.Headers.TryAddWithoutValidation("Accept","application/json, text/event-stream");
                req.Content=new StringContent(Request(token:"quiet-test"),Encoding.UTF8,"application/json");
                using var response=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead);
                using var reader=new StreamReader(await response.Content.ReadAsStreamAsync());
                string? line;var heartbeat=false;
                while((line=await reader.ReadLineAsync()) is not null)
                    if(line==": keep-alive"){heartbeat=true;break;}
                Require(heartbeat && !probe.Completed && hub.Snapshot().Count==2,"heartbeat became work progress or was missing");
                probe.Gate.TrySetResult();await reader.ReadToEndAsync();
            }
            finally{probe.Gate.TrySetResult();await server.StopAsync();}
        });
        var failed=Results.Count(x=>JsonSerializer.Serialize(x).Contains("\"result\":\"FAIL\""));
        File.WriteAllText(Path.Combine(Run,"results.json"),JsonSerializer.Serialize(new { kind="PROGRESS_INTEGRATION_CHECKS_NOT_GIS_ACCEPTANCE",results=Results,failed },new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine($"RESULT {Results.Count-failed}/{Results.Count}; {Run}");return failed==0?0:1;
    }

    private static async Task Demo()
    {
        var hub=new ToolProgressHub();var directory=Path.Combine(Environment.GetEnvironmentVariable("PROGRESS_CHECK_ROOT")
            ?? @"D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel","events");hub.ConfigureJournal(directory);
        var probe=new Probe { FileDemo=true };var transport=new HttpMcpTransport("127.0.0.1",16524,"/mcp",NullLogger.Instance);
        var server=Server(probe,hub,transport,timeout:60000);await server.StartAsync();
        try
        {
            using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(60)};
            using var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:16524/mcp");
            req.Headers.TryAddWithoutValidation("Accept","application/json, text/event-stream");
            req.Content=new StringContent(Request(token:"development-file-check-not-gis"),Encoding.UTF8,"application/json");
            using var response=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead);
            using var reader=new StreamReader(await response.Content.ReadAsStreamAsync());
            await using var capture=new StreamWriter(Path.Combine(Run,"actual-sse-transcript.txt"),false,Encoding.UTF8);
            string? line;while((line=await reader.ReadLineAsync()) is not null){await capture.WriteLineAsync(line);await capture.FlushAsync();}
            File.WriteAllText(Path.Combine(Run,"demo-result.json"),JsonSerializer.Serialize(new {kind="ACTUAL_LOCAL_FILE_HASH_FIXTURE_NOT_PRODUCTION_GIS",events=hub.Snapshot().Count,probe.Calls,probe.Completed,journal=hub.JournalPath}));
            Console.WriteLine("Demo ended; fixture HTTP listener stopped. Evidence "+Run);
        }
        finally{await server.StopAsync();}
    }

    private sealed class Probe : IMCPTool
    {
        public string Name=>"progress_probe";
        public string Description=>"Development fixture only, never registered in production. No GIS operations.";
        public IReadOnlyDictionary<string,object?> InputSchema=>new Dictionary<string,object?> { ["type"]="object",["properties"]=new Dictionary<string,object?>() };
        public TaskCompletionSource? Gate;public bool WaitForever;public bool FileDemo;public int Calls;public bool Completed;
        public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        {
            Calls++;if(Gate is not null)await Gate.Task.WaitAsync(context.CancellationToken);
            if(WaitForever)await Task.Delay(Timeout.Infinite,context.CancellationToken);
            if(FileDemo)
            {
                var files=Directory.EnumerateFiles(@"D:\ArcGIS-Pro-MCP 2.0\Source\Shared","*.cs",SearchOption.AllDirectories)
                    .Where(p=>!p.Contains("\\obj\\") && !p.Contains("\\bin\\")).Take(40).ToArray();
                for(var i=0;i<files.Length;i++)
                {
                    await using var file=File.OpenRead(files[i]);_ = await SHA256.HashDataAsync(file,context.CancellationToken);
                    ToolProgress.Report("fixture:file-hashes","RUNNING",$"开发验收夹具 · 文件校验 {i+1}/{files.Length}（非 GIS 生产任务）",i+1,files.Length,"fixture.actual-file-hash");
                    await Task.Delay(400,context.CancellationToken); // Explicit viewing delay, not a performance measurement.
                }
            }
            else{ToolProgress.Report("probe:rows","RUNNING","真实测试行 1/2",1,2,"test.fixture");await Task.Yield();ToolProgress.Report("probe:rows","RUNNING","真实测试行 2/2",2,2,"test.fixture");}
            Completed=true;return OperationResult<object?>.Ok(new {kind="TEST_FIXTURE_NOT_PRODUCTION_GIS",completed=Completed});
        }
    }
}
