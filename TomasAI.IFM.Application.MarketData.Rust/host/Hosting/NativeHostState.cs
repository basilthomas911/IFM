using TomasAI.IFM.Application.MarketData.Rust.Publication;
using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Host-local logical ownership and recovery observations. ProcessId is
/// the real Host PID for every Tokio dataset; worker IDs identify logical lanes,
/// never fabricated child processes. State contains at most three roles.</summary>
public sealed class NativeHostState(NativeMarketDataEpochFactory epochs,DatasetWorkerAdmissionRegistry admissions,
    TimeProvider clock,ILogger<NativeHostState> logger, TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth processing)
{
    readonly object gate=new();
    readonly Dictionary<string,Guid> owners=new(StringComparer.Ordinal);
    readonly Dictionary<string,Guid> receiverFailures=new(StringComparer.Ordinal);
    readonly Dictionary<string,MarketDataDatasetStatus> recoveryStates=new(StringComparer.Ordinal);
    readonly Dictionary<string,LiveLaneStatus> scopeStates=new(StringComparer.Ordinal);
    readonly Queue<string> scopeOrder=new();
    Guid observationEpoch;
    DatasetSubscriptionManifest[] primaryManifests=[];
    DateOnly? ownerDate;bool closing;long revision;long optionManifestRevision=1;
    /// <summary>Identity of this actual Host lifetime.</summary>
    public Guid HostInstanceId{get;}=Guid.NewGuid();
    /// <summary>Complete native diagnostic sinks. Unknown processing failures
    /// become typed health evidence for the existing API recovery supervisor.</summary>
    public NativeEpochObservers Observers
    {
        get
        {
            Guid stamp;lock(gate)stamp=observationEpoch;
            return new(
                diagnostic=>logger.Log(diagnostic.Kind==2?LogLevel.Error:diagnostic.Code==4?LogLevel.Warning:LogLevel.Information,"Native feed diagnostic; Lane={Lane}; Kind={Kind}; Code={Code}; Attempt={Attempt}; Last={Last}; Message={Message}",diagnostic.Lane,diagnostic.Kind,diagnostic.Code,diagnostic.Attempt,diagnostic.Last,diagnostic.Message),
                warning=>logger.LogWarning("Native provider warning; Endpoint={Endpoint}; Status={Status}; RequestId={RequestId}; Message={Message}",warning.Endpoint,warning.Status,warning.RequestId,warning.Text),
                (scope,status)=>ObserveScope(stamp,scope,status),
                (scope,error)=>ReportProcessingFailure(scope??"NativeReceiver",error,stamp));
        }
    }
    void ObserveScope(Guid stamp,string scope,LiveLaneStatus status)
    {
        lock(gate)
        {
            if(stamp!=observationEpoch)return;
            if(scopeStates.TryGetValue(scope,out var previous)&&previous==status)return;
            if(!scopeStates.ContainsKey(scope)){if(scopeStates.Count>=64)scopeStates.Remove(scopeOrder.Dequeue());scopeOrder.Enqueue(scope);}
            scopeStates[scope]=status;
        }
        logger.Log(status.Phase==5?LogLevel.Error:status.Phase==4?LogLevel.Warning:LogLevel.Information,"Native option lifecycle; Scope={Scope}; Phase={Phase}; Episode={Episode}; Attempts={Attempts}; Error={Error}",scope,status.Phase,status.Episode,status.Attempts,status.Error);
    }
    /// <summary>Retains unknown exceptions before logging, independently of log
    /// delivery. A retired epoch's late callback cannot condemn its replacement.</summary>
    public void ReportProcessingFailure(string component,Exception error)=>ReportProcessingFailure(component,error,null);
    void ReportProcessingFailure(string component,Exception error,Guid? expectedEpoch)
    {
        DateOnly? date;lock(gate){if(closing||expectedEpoch is{} expected&&expected!=observationEpoch)return;date=ownerDate;}
        if(date is{} value)
        {
            if(error is NativeDeliveryFailure{LaneMask:>0} delivery&&epochs.TryGetCurrent(out var native))
            {
                var failures=native!.GetHealth().DatasetFeedStatuses??[];
                foreach(var item in failures.Where(x=>(delivery.LaneMask&(x.Dataset=="GLBX.MDP3"?1u:2u))!=0))
                {
                    var status=new MarketDataDatasetStatus("Futures",item.Dataset,Owner(value,"Futures",item.Dataset),item.GenerationId,Environment.ProcessId,true,false,"Recovering",error.Message,value,Guid.NewGuid(),ChangedAtUtc:clock.GetUtcNow(),RecoveryDeadlineUtc:clock.GetUtcNow().AddMinutes(2));
                    lock(gate){var key=status.Role+"|"+status.Dataset;recoveryStates[key]=status;if(delivery.Component=="Receiver")receiverFailures[key]=status.OperationId;else receiverFailures.Remove(key);revision++;}
                }
            }
            if(error is not NativeDeliveryFailure{Component:"Primary"})processing.Report(value,epochs.TryGetCurrent(out var epoch)?epoch!.OptionGeneration:Guid.Empty,component,error);
        }
        try{logger.LogError(error,"Native feed processing failed; Component={Component}",component);}
        catch(Exception logging){if(date is{} logDate)processing.Report(logDate,epochs.TryGetCurrent(out var active)?active!.OptionGeneration:Guid.Empty,"NativeLogging",logging);}
    }
    /// <summary>Captures exact shared-receiver failure episodes for a repair.
    /// A later, independent futures fault cannot be cleared by that repair.</summary>
    public IReadOnlyDictionary<string,Guid> CaptureReceiverFailures(){lock(gate)return new Dictionary<string,Guid>(receiverFailures);}
    /// <summary>Clears only the repaired receive owner's prior failure evidence.</summary>
    public void DeliveryRecovered(IReadOnlyDictionary<string,Guid> repaired)
    {
        lock(gate)foreach(var item in repaired)
            if(recoveryStates.TryGetValue(item.Key,out var failure)&&failure.OperationId==item.Value)
            {recoveryStates.Remove(item.Key);receiverFailures.Remove(item.Key);revision++;}
    }
    /// <summary>Retires failure evidence only after verified native and managed readiness.</summary>
    public void ProcessingRecovered(DateOnly date,Guid generation,DateTimeOffset through)=>processing.Recovered(date,generation,through);
    /// <summary>Current native session date, or null before/after its lifecycle.</summary>
    public DateOnly? ValueDate=>epochs.TryGetCurrent(out var epoch)?epoch!.ValueDate:null;
    /// <summary>Explicit startup transition. Late publishers cannot select a
    /// different date and silently clear the current ownership directory.</summary>
    public void PrepareDate(DateOnly date,IReadOnlyCollection<DatasetSubscriptionManifest>? manifests=null)
    {
        if(date==default)throw new ArgumentOutOfRangeException(nameof(date));
        lock(gate)
        {
            closing=false;observationEpoch=Guid.NewGuid();scopeStates.Clear();scopeOrder.Clear();
            primaryManifests=manifests?.ToArray()??[];
            optionManifestRevision=primaryManifests.SingleOrDefault(x=>x.Dataset=="GLBX.MDP3")?.Revision??1;
            if(ownerDate==date)return;owners.Clear();recoveryStates.Clear();receiverFailures.Clear();ownerDate=date;revision++;
        }
    }
    /// <summary>Returns the fixed futures manifests for API mirror admission.
    /// Later option-only underlying additions cannot alter futures revisions.</summary>
    public DatasetSubscriptionManifest[] CapturePrimaryManifests(){lock(gate)return primaryManifests.ToArray();}
    /// <summary>Gets one stable logical owner for a role/dataset/date.</summary>
    public Guid Owner(DateOnly date,string role,string dataset)
    {
        if(date==default||role is not("Futures" or "Options")||dataset is not("GLBX.MDP3" or "XCBF.PITCH")||role=="Options"&&dataset!="GLBX.MDP3")throw new ArgumentException("Unknown native dataset role.");
        lock(gate)
        {
            if(ownerDate!=date)throw new InvalidOperationException("Native publication belongs to a retired or unprepared API ValueDate.");
            var key=role+"|"+dataset;
            if(!owners.TryGetValue(key,out var id)){if(owners.Count>=3)throw new InvalidOperationException("Native Host role capacity exhausted.");owners[key]=id=Guid.NewGuid();revision++;}
            return id;
        }
    }
    /// <summary>Commits a new logical options owner only after native cleanup
    /// and restoration returned. The actual Host PID remains unchanged.</summary>
    public Guid CommitOptionReplacement(DateOnly date,Guid oldOwner,Guid generation)
    {
        lock(gate)
        {
            const string key="Options|GLBX.MDP3";
            if(ownerDate!=date||!owners.TryGetValue(key,out var current)||current!=oldOwner)
                throw new InvalidOperationException("Native options owner changed during replacement.");
            if(generation==Guid.Empty)throw new ArgumentException("Native generation is absent.",nameof(generation));
            var next=Guid.NewGuid();owners[key]=next;recoveryStates.Remove(key);revision++;
            admissions.Admit(new("GLBX.MDP3",date,next,generation,optionManifestRevision));
            return next;
        }
    }
    /// <summary>Closes application session metadata without shutting a mailbox.
    /// Background reference owners release their scopes before native teardown.</summary>
    public void CloseOptionsAdmission(DateOnly date)
    {
        lock(gate){if(ownerDate!=date)return;closing=true;revision++;}
        if(admissions.TryGet("GLBX.MDP3",out var previous)&&previous.ValueDate==date)admissions.Close(previous.Dataset,previous.GenerationId);
    }
    /// <summary>Commits a fully qualified native option-underlying manifest.</summary>
    public void OptionManifestApplied(long value)
    {if(value<1)throw new ArgumentOutOfRangeException(nameof(value));lock(gate){if(value<optionManifestRevision)throw new InvalidOperationException("Option manifest revision regressed.");optionManifestRevision=value;revision++;}}
    /// <summary>Retains the authoritative recovery state for API/UI observation.</summary>
    public void RecoveryChanged(MarketDataDatasetStatus status)
    {
        lock(gate){var key=status.Role+"|"+status.Dataset;if(status.Lifecycle=="Healthy")recoveryStates.Remove(key);else recoveryStates[key]=status;revision++;}
        if(status.Lifecycle=="StoppedError")logger.LogError("Native market-data recovery failed; Role={Role}; Dataset={Dataset}; Operation={Operation}; Error={Error}",status.Role,status.Dataset,status.OperationId,status.Error);
        else logger.LogInformation("Native market-data recovery state; Role={Role}; Dataset={Dataset}; State={State}; Operation={Operation}",status.Role,status.Dataset,status.Lifecycle,status.OperationId);
    }
    /// <summary>Observes native state without provider I/O. Empty option scope is
    /// healthy idle, not evidence that an option chain is trading-ready.</summary>
    public MarketDataBackendStatus Capture(bool hostReady,string[]? enabledTrades=null)
    {
        lock(gate)if(closing)return new(hostReady,null,[],HostInstanceId,revision,RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner);
        if(!epochs.TryGetCurrent(out var epoch))return new(hostReady,null,[],HostInstanceId,Volatile.Read(ref revision),EnabledOptionTrades:enabledTrades,RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner);
        var date=epoch!.ValueDate;var health=epoch.GetHealth();var output=new List<MarketDataDatasetStatus>();
        foreach(var item in health.DatasetFeedStatuses??[])
        {
            var healthy=item.Health.State==FeedState.Running;
            output.Add(new("Futures",item.Dataset,Owner(date,"Futures",item.Dataset),item.GenerationId,Environment.ProcessId,true,healthy,
                healthy?"Healthy":item.Health.State==FeedState.Faulted?"StoppedError":"Recovering",healthy?null:item.Health.Warning,date,ManifestRevision:CapturePrimaryManifests().SingleOrDefault(x=>x.Dataset==item.Dataset)?.Revision??1,ChangedAtUtc:clock.GetUtcNow()));
        }
        var scopes=epoch.GetOptionScopeStates().Values;var terminal=scopes.Any(x=>x.Phase==5);var optionsHealthy=!epoch.IsOptionOwnerReplacementPending&&scopes.All(x=>x.Phase==3);
        var option=new MarketDataDatasetStatus("Options","GLBX.MDP3",Owner(date,"Options","GLBX.MDP3"),epoch.OptionGeneration,Environment.ProcessId,true,optionsHealthy,
            terminal?"StoppedError":optionsHealthy?"Healthy":"Recovering",scopes.FirstOrDefault(x=>x.Error is not null)?.Error,date,ManifestRevision:Interlocked.Read(ref optionManifestRevision),ChangedAtUtc:clock.GetUtcNow());
        lock(gate)
        {
            if(recoveryStates.TryGetValue("Options|GLBX.MDP3",out var saved)&&saved.GenerationId==option.GenerationId)option=saved;
            output.Add(option);
            for(var i=0;i<output.Count;i++)if(recoveryStates.TryGetValue(output[i].Role+"|"+output[i].Dataset,out var savedState)&&savedState.GenerationId==output[i].GenerationId)output[i]=savedState;
        }
        // This registry is used solely for application reference/ownership identity.
        // Native connection recovery does not close an actor mailbox or quarantine
        // data admission; physical native generations fence retired observations.
        if(option.Lifecycle=="Healthy")admissions.Admit(new(option.Dataset,date,option.WorkerInstanceId,option.GenerationId,option.ManifestRevision));
        return new(hostReady,date,output,HostInstanceId,Volatile.Read(ref revision),OptionProcessingFailure:processing.Read(date,epoch.OptionGeneration),EnabledOptionTrades:enabledTrades,RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner);
    }
}
