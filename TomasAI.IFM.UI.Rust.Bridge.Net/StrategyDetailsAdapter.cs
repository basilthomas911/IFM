using TomasAI.IFM.Domain.Trade.Shared;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
[MessagePack.MessagePackObject]
public sealed record PropertyRow([property: MessagePack.Key("name")] string Name, [property: MessagePack.Key("value")] string Value);
[MessagePack.MessagePackObject]
public sealed record ObjectNode([property: MessagePack.Key("id")] string Id, [property: MessagePack.Key("label")] string Label, [property: MessagePack.Key("properties")] PropertyRow[] Properties, [property: MessagePack.Key("children")] ObjectNode[] Children);
[MessagePack.MessagePackObject]
public sealed record WorkflowChoice([property: MessagePack.Key("id")] string Id, [property: MessagePack.Key("label")] string Label, [property: MessagePack.Key("action")] uint Action);
[MessagePack.MessagePackObject]
public sealed record WorkflowActor([property: MessagePack.Key("name")] string Name, [property: MessagePack.Key("value")] string Value, [property: MessagePack.Key("state")] string State);
[MessagePack.MessagePackObject]
public sealed record WorkflowDisplayRow([property: MessagePack.Key("id")] string Id, [property: MessagePack.Key("action")] uint Action, [property: MessagePack.Key("triggerIdentity")] string TriggerIdentity, [property: MessagePack.Key("occurredOn")] string OccurredOn, [property: MessagePack.Key("signal")] string Signal, [property: MessagePack.Key("trend")] string Trend, [property: MessagePack.Key("futuresPrice")] string FuturesPrice, [property: MessagePack.Key("outcome")] string Outcome, [property: MessagePack.Key("endState")] string EndState, [property: MessagePack.Key("actors")] WorkflowActor[] Actors);
[MessagePack.MessagePackObject]
public sealed record StrategyGraphPoint([property: MessagePack.Key("identity")] string Identity, [property: MessagePack.Key("utcSeconds")] double UtcSeconds, [property: MessagePack.Key("price")] double Price, [property: MessagePack.Key("direction")] string Direction, [property: MessagePack.Key("tooltip")] string Tooltip) { [MessagePack.Key("marketSeconds")] public double MarketSeconds {get;init;} } ;
[MessagePack.MessagePackObject]
public sealed record StrategySummarySection([property: MessagePack.Key("title")] string Title, [property: MessagePack.Key("state")] string State, [property: MessagePack.Key("summary")] string Summary, [property: MessagePack.Key("content")] string Content);
[MessagePack.MessagePackObject]
public sealed record DetailSnapshot([property: MessagePack.Key("schema")] int Schema, [property: MessagePack.Key("epoch")] string Epoch, [property: MessagePack.Key("generation")] long Generation, [property: MessagePack.Key("mode")] string Mode, [property: MessagePack.Key("workflowId")] string WorkflowId, [property: MessagePack.Key("revision")] long Revision, [property: MessagePack.Key("roots")] ObjectNode[] Roots) { [MessagePack.Key("workflows")] public WorkflowChoice[] Workflows {get;init;}=[]; [MessagePack.Key("status")] public string Status {get;init;}="Ready"; [MessagePack.Key("rows")] public WorkflowDisplayRow[] Rows {get;init;}=[]; [MessagePack.Key("signals")] public StrategyGraphPoint[] Signals {get;init;}=[]; [MessagePack.Key("summary")] public StrategySummarySection[] Summary {get;init;}=[]; [MessagePack.Key("header")] public string Header {get;init;}=""; [MessagePack.Key("pageNumber")] public int PageNumber {get;init;}=1; [MessagePack.Key("pageCount")] public int PageCount {get;init;}=1; [MessagePack.Key("totalRows")] public int TotalRows {get;init;} [MessagePack.Key("hasMore")] public bool HasMore {get;init;} [MessagePack.Key("graphStartUtc")] public double GraphStartUtc {get;init;} [MessagePack.Key("graphEndUtc")] public double GraphEndUtc {get;init;} [MessagePack.Key("graphStartMarket")] public double GraphStartMarket {get;init;} [MessagePack.Key("graphEndMarket")] public double GraphEndMarket {get;init;} [MessagePack.Key("contractId")] public string ContractId {get;init;}=""; [MessagePack.Key("timeFrame")] public string TimeFrame {get;init;}="Daily"; }
/// <summary>Projects existing immutable C# workflow details outside the Rust rendering thread.</summary>
public sealed class StrategyDetailsAdapter : IDisposable
{
    FixtureViewModelSession? fixture;
    public void Dispose()=>fixture?.Dispose();
    readonly string epoch = Guid.NewGuid().ToString("D");
    readonly Guid[] identities = [Guid.Parse("d679aa2a-154d-4cf3-9078-3de29e97e601"), Guid.Parse("d679aa2a-154d-4cf3-9078-3de29e97e602")];
    public DetailSnapshot CreateFixture(int selected, long generation)
    {
        var view = new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(new TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignalEntityId("ESZ26",DateOnly.FromDateTime(DateTime.UtcNow),TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily)),
            WorkflowId = new StrategyWorkflowId(identities[selected]), WorkflowRevision = generation,
            StartedAtUtc = DateTime.UtcNow.AddSeconds(-1), UpdatedAtUtc = DateTime.UtcNow.AddSeconds(-1),
            RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Completed, ContinuationReasonCodes = ["fixture"], Result = new() { ResultId = identities[selected], Payload = new byte[] {1,2,3} } },
            MarketCondition = new() { ProcessingStatus = StrategyActorProcessingStatus.Processing }
        };
        var details=(fixture??=new()).Select(view);
        var row=StrategyWorkflowPresentation.CreateRow(view);
        return Project(details, generation) with {Rows=[Display(row, (uint)selected)],TotalRows=1};
    }
    public DetailSnapshot Project(StrategyWorkflowDetails details, long generation)
    {
        int budget=10000;
        var view=details.Workflow;
        var prefix=details.WorkflowId.ToString();
        var roots=new List<ObjectNode>{Build(prefix+"/workflow", "Workflow", (object?)view ?? details, 0, [], ref budget, true),
            Build(prefix+"/signal", "ITI Signal", (object?)view?.TriggerEvent ?? details.Sections.FirstOrDefault(s=>s.Key=="iti"),0,[],ref budget)};
        object?[] stages=[view?.RegimeDiscovery,view?.MarketCondition,view?.TradeSelection,view?.OrderComposition,view?.RiskManagement];
        var sections=details.Sections.Where(s=>s.Key!="iti").ToArray();
        roots.Add(new(prefix+"/pipeline","Workflow Pipeline",[],sections.Select((s,i)=>(s,i)).Select(pair=>BuildStage(pair.s,pair.i)).ToArray()));
        return new(1,epoch,generation,"Fixture",prefix,details.WorkflowRevision,roots.ToArray()){Header=details.Header};
        ObjectNode BuildStage(StrategyWorkflowDetailSection section,int index) => Build(prefix+"/pipeline/"+section.Key,section.Title,index<stages.Length?stages[index]??section:section,0,[],ref budget);
    }
    public static string FormatWorkflowTime(DateTime occurredOn, TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType timeFrame)
        => TomasAI.IFM.UI.Net.Models.EasternTime.FromUtc(occurredOn).ToString(
            timeFrame == TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily ? "hh:mm:ss.fff tt" : "dd-MMM-yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture);
    public static WorkflowDisplayRow Display(StrategyWorkflowRow row,uint action)=>new(row.WorkflowId.ToString(),action,row.TriggerStableIdentity,FormatWorkflowTime(row.OccurredOn,row.TimePeriod),row.SignalEvent.ToString(),row.Trend.ToString(),row.FuturesPrice.ToString("F2",CultureInfo.InvariantCulture),row.Outcome.ToString(),row.EndState,row.PipelineActors.Select(a=>new WorkflowActor(a.ShortLabel,a.AccessibleStatus,a.DisplayState.ToString())).ToArray());
    // Plot coordinates represent Eastern wall-clock time; the UTC instant remains authoritative.
    public static double MarketSeconds(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(TomasAI.IFM.UI.Net.Models.EasternTime.FromUtc(utc), DateTimeKind.Utc)).ToUnixTimeMilliseconds()/1000d;
    public static StrategyGraphPoint Graph(FuturesItiSignalEventRow row)=>new(row.StableIdentity,new DateTimeOffset(DateTime.SpecifyKind(row.OccurredOn,DateTimeKind.Utc)).ToUnixTimeMilliseconds()/1000d,row.IntrinsicPrice,row.Mode==TomasAI.IFM.Domain.MarketData.Analytics.Shared.IntrinsicTimeModeType.TrendDirectionChanged?(row.Trend==TomasAI.IFM.Domain.MarketData.Analytics.Shared.IntrinsicTimeTrendType.UpTrend?"Up":"Down"):"Other",$"{TomasAI.IFM.UI.Net.Models.EasternTime.FromUtc(row.OccurredOn):yyyy-MM-dd h:mm:ss tt} ET | {row.Mode} | {row.IntrinsicPrice.ToString("F2",CultureInfo.InvariantCulture)}") { MarketSeconds = MarketSeconds(row.OccurredOn) };
    static bool Scalar(Type t) => t.IsPrimitive || t.IsEnum || t==typeof(string) || t==typeof(decimal) || t==typeof(Guid) || t==typeof(StrategyWorkflowId) || t==typeof(DateTime) || t==typeof(DateOnly) || t==typeof(TimeSpan);
    static ObjectNode Build(string id,string label,object? value,int depth,List<object> ancestors,ref int budget,bool root=false)
    {
        if (--budget<0 || depth>12) return new(id,label+" (display limit)",[],[]);
        if(value is null) return new(id,label,[new("Value","Unavailable")],[]);
        if(Scalar(value.GetType())) return new(id,label,[new("Value",Format(value))],[]);
        if(ancestors.Any(x=>ReferenceEquals(x,value))) return new(id,label,[new("Value","Cycle omitted")],[]);
        ancestors.Add(value);
        var rows=new List<PropertyRow>();var children=new List<ObjectNode>();
        if(value is IEnumerable seq && value is not string)
        {
            int i=0;foreach(var item in seq) { if(i>=200||budget<=0){rows.Add(new("Display limit","Additional items omitted"));break;} children.Add(Build(id+"/"+i,$"[{i++}]",item,depth+1,ancestors,ref budget)); }
        }
        else foreach(PropertyDescriptor p in TypeDescriptor.GetProperties(value))
        {
            if(!p.IsBrowsable || p.Name=="StageSummary" || (root && p.Name is "RegimeDiscovery" or "MarketCondition" or "TradeSelection" or "OrderComposition" or "RiskManagement" or "TriggerEvent" or "Sections")) continue;
            object? child;try{child=p.GetValue(value);}catch(Exception e){rows.Add(new(p.DisplayName,"Unavailable: "+e.GetBaseException().Message));continue;}
            rows.Add(new(p.DisplayName,child is null?"null":Scalar(child.GetType())?Format(child):child.GetType().Name));
            if(child is not null&&!Scalar(child.GetType())&&budget>0) children.Add(Build(id+"/"+p.Name,p.DisplayName,child,depth+1,ancestors,ref budget));
        }
        ancestors.RemoveAt(ancestors.Count-1);
        return new(id,label,rows.ToArray(),children.ToArray());
    }
    static string Format(object value) => value switch {DateTime d=>d.ToString("O",CultureInfo.InvariantCulture),IFormattable f=>f.ToString(null,CultureInfo.InvariantCulture),_=>value.ToString()??""};
}


