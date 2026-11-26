using System.Data.Common;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;
/// <summary>Read-only replay configuration evidence. Credentials remain in protected environment/providers.</summary>
public static class SessionCaptureConfiguration
{
    public sealed record Evidence(int Version,string Kind,IReadOnlyDictionary<string,string?> Settings,IReadOnlyDictionary<string,string> OwnerPaths,bool AllOwnerPathsExplicit,DateTimeOffset ObservedUtc)
    { public IReadOnlyList<SessionCaptureRuntimeEvidence.AssemblyEvidence> RuntimeAssemblies {get;init;}=[]; }
    public static IReadOnlyDictionary<string,string> EffectiveOwnerPaths(IConfiguration config)
    {
        string Local(string name)=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IFM",name);
        var defaults=new Dictionary<string,string>{["Actors:RealtimeMailboxes:Directory"]=new TomasAI.IFM.Shared.EventModelActor.RealtimeMailboxOptions().Directory,
            ["RawFuturesStorage:Directory"]=new TomasAI.IFM.Application.MarketData.Storage.RawFuturesStorageOptions().Directory,
            ["TradeBroker:Emulator:LedgerPath"]=Path.Combine(AppContext.BaseDirectory,"data","trade-broker","emulator-ledger.json"),
            ["ResidentActorPilots:FuturesItiJournalDirectory"]=new TomasAI.IFM.Shared.EventModelActor.ResidentActorPilotOptions().FuturesItiJournalDirectory};
        string[] names=["FuturesOptionRouting","FuturesRouting","DailyFuturesExit","WorkflowInitialization","WorkflowComposition","WorkflowMarketCondition","WorkflowReservation","WorkflowCompositionFunction","WorkflowSelectionFunction","WorkflowAssessmentFunction","WorkflowRegimeFunction","WorkflowPublication","TradeSessionBar","ItiIngress","TdiResult","IronCondorPosition"];
        string[] paths=["futures-option-routing-receipts","futures-routing-receipts","daily-futures-exit-intents","workflow-initialization-terminals","workflow-composition-failures","workflow-market-condition-failures","workflow-reservation-failures","workflow-composition-function-terminals","workflow-selection-function-failures","workflow-assessment-function-failures","workflow-regime-function-failures","workflow-publications","trade-session-bars","iti-ingress","tdi-results","iron-condor-position-requests"];
        for(var i=0;i<names.Length;i++)defaults["AppSettings:"+names[i]+"Recovery:Directory"]=Local(paths[i]);
        return HistoricalSessionReplay.OwnerPathKeys.ToDictionary(key=>key,key=>Path.GetFullPath(config[key]??defaults[key]),StringComparer.Ordinal);
    }
    public static Evidence Export(IConfiguration config,TimeProvider? clock=null)
    {
        var settings=new SortedDictionary<string,string?>(StringComparer.Ordinal);var owners=EffectiveOwnerPaths(config);
        foreach(var item in config.AsEnumerable())
        {
            if(item.Value is null)continue;
            if(item.Key.StartsWith("ConnectionStrings:",StringComparison.OrdinalIgnoreCase))
            {
                var connection=new DbConnectionStringBuilder{ConnectionString=item.Value};
                foreach(var key in connection.Keys.Cast<string>().Where(key=>new[]{"user id","userid","username","user","password","pwd","pass","access token"}.Contains(key,StringComparer.OrdinalIgnoreCase)).ToArray())connection.Remove(key);
                settings[item.Key]=connection.ConnectionString;continue;
            }
            var lower=item.Key.ToLowerInvariant();
            if(lower.Contains("password")||lower.Contains("secret")||lower.Contains("token")||lower.Contains("credential")||lower.Contains("apikey")||lower.Contains("api_key")||lower.EndsWith(":key",StringComparison.Ordinal)||lower.EndsWith("_key",StringComparison.Ordinal)||lower.Contains("connectionstring"))continue;
            if(new[]{"AppSettings:","Actors:","RawFuturesStorage:","ResidentActorPilots:","TradeBroker:","MarketDataHost:","Nats:","StageExecutors:","StartupOrchestration:","ApplicationStartup:","ActorRuntime:","MarketData:","MarketDataRecovery:","Supervisor:","ParameterSets:","EventProjectorReliability:","Storage:","EventLogPersistence:","CommandAuditPersistence:","InMemoryEventSourceActor:","DeploymentIdentity:","Portfolio:"}.Any(prefix=>item.Key.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)))settings[item.Key]=item.Value;
        }
        foreach(var owner in owners)settings[owner.Key]=owner.Value;
        return new Evidence(1,"SanitizedEffectiveReplayConfiguration",settings,owners,HistoricalSessionReplay.OwnerPathKeys.All(key=>!string.IsNullOrWhiteSpace(config[key])),(clock??TimeProvider.System).GetUtcNow())
            {RuntimeAssemblies=SessionCaptureRuntimeEvidence.Capture()};
    }
}
