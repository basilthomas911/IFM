using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using TomasAI.IFM.UI.Rust.Bridge.Net;

if(args.Length>0&&args[0]=="--trade-probe") {await TradeMonitorProbe.RunAsync(args[1]);return;}
if(args.Length>0&&args[0]=="--probe") {await BridgeProbe.RunAsync(args[1]);return;}
var config=ManualConfig.Create(DefaultConfig.Instance);
foreach(var server in new[]{false,true}) config.AddJob(Job.Default.WithId(server?"Server":"Workstation")
    .WithGcServer(server).WithLaunchCount(1).WithWarmupCount(2).WithIterationCount(5)
    .WithIterationTime(TimeInterval.FromMilliseconds(150)));
var summaries=BenchmarkSwitcher.FromAssembly(typeof(BridgeBenchmarks).Assembly).Run(args,config);
if(summaries.Any(summary=>summary.Reports.Any(report=>!report.Success)||summary.ValidationErrors.Any(error=>error.IsCritical)))Environment.ExitCode=1;

[MemoryDiagnoser]
public class BridgeBenchmarks
{
    [Params(100,1000)] public int Rows {get;set;}
    ContractEditorGateway gateway=null!;
    object document=null!;
    [GlobalSetup] public void Setup() {
        var rows=Enumerable.Range(0,Rows).Select(i=>new ContractEditorRow(i.ToString(),new(){["kind"]="option"}) {
            Data=BridgeDocument.Encode(new{contractId=$"ES-C-{5000+i}",strike=5000+i,bid=2.1,ask=2.3,delta=.16,volume=1000,openInterest=5000})}).ToArray();
        var result=new ContractEditorResult("option-contract",rows,new(){["expiry"]=["2026-11-20"]},Context:new(){["underlying"]="ES"});
        gateway=new(_=>Task.FromResult(result));
        gateway.Submit(new(1,Guid.NewGuid(),"option-contract","load",1,null,[]));gateway.DrainAsync().GetAwaiter().GetResult();
        document=new{contracts=Enumerable.Range(0,Rows).Select(i=>new{contractId=$"ES-C-{5000+i}",strike=5000+i,bid=2.1,ask=2.3,delta=.16,volume=1000}).ToArray()};
        _=gateway.WireSnapshot();
    }
    [Benchmark] public byte[] ReadPublication()=>gateway.WireSnapshot();
    [Benchmark] public byte[] EncodeMarketDocument()=>BridgeDocument.Encode(document,true);
    [Benchmark] public Task RefreshPublication()=>Refresh();
    async Task Refresh(){gateway.TryRefresh("option-contract",()=>Task.FromResult(gateway.Snapshot().Result));await gateway.DrainAsync();_=gateway.WireSnapshot();}
}
