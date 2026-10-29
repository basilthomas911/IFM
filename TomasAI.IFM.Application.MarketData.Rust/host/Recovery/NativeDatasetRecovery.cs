using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Adapts public recovery requests to Tokio ownership. It never launches
/// a process, drains old market data, or restarts an expired native incident with
/// a fresh deadline. Terminal state is retained for existing API/UI escalation.</summary>
public sealed class NativeDatasetRecovery(NativeMarketDataEpochFactory epochs,NativeHostState state,TimeProvider clock,NativeHostApplicationServices application)
{
    readonly NativeHostRecovery episodes=new(clock,state.RecoveryChanged);
    readonly object completedGate=new();
    readonly Dictionary<string,(Guid Operation,DateOnly Date,MarketDataRecoveryResult Result)> completed=new(StringComparer.Ordinal);
    MarketDataRecoveryResult Remember(MarketDataRecoveryRequest request,MarketDataRecoveryResult result)
    {
        // One retained terminal result per supported role/dataset (at most
        // three), not an unbounded request history. Re-observing the same
        // incident cannot repeatedly destroy a successfully restored owner.
        lock(completedGate)completed[request.Role+"|"+request.Dataset]=(request.OperationId,request.ValueDate,result);
        return result;
    }
    /// <summary>Recovers only the exact requested logical owner and API ValueDate.</summary>
    public async Task<MarketDataRecoveryResult> RecoverAsync(MarketDataRecoveryRequest request,CancellationToken token=default)
    {
        var snapshot=state.Capture(true);var current=snapshot.Datasets.SingleOrDefault(x=>x.Role==request.Role&&x.Dataset==request.Dataset)
            ??throw new InvalidOperationException("Requested native dataset role is not active.");
        if(current.ValueDate!=request.ValueDate)throw new InvalidOperationException("Recovery ValueDate differs from its native owner.");
        lock(completedGate)
            if(completed.TryGetValue(request.Role+"|"+request.Dataset,out var previous)&&previous.Operation==request.OperationId&&previous.Date==request.ValueDate
                &&(!previous.Result.Recovered||current.Healthy&&snapshot.OptionProcessingFailure is null
                    &&current.WorkerInstanceId==previous.Result.WorkerInstanceId&&current.GenerationId==previous.Result.GenerationId))
                return previous.Result;
        if(episodes.TryJoin(request,out var pending))return (await pending!.WaitAsync(token).ConfigureAwait(false)) with{OperationId=request.OperationId};
        if(current.WorkerInstanceId!=request.ExpectedWorkerInstanceId||current.GenerationId!=request.ExpectedGenerationId)
            return new(request.OperationId,current.Role,current.Dataset,current.Healthy,true,current.Running,"Stale native recovery ignored.",current.WorkerInstanceId,current.GenerationId);
        var epoch=epochs.Current;
        if(current.Role=="Futures")
        {
            var budget=request.DeadlineUtc is{} deadline?deadline-clock.GetUtcNow():TimeSpan.FromMinutes(2);
            budget=budget<TimeSpan.Zero?TimeSpan.Zero:budget>TimeSpan.FromMinutes(2)?TimeSpan.FromMinutes(2):budget;
            using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);bounded.CancelAfter(budget);
            var result=await epoch.ResetDatasetAsync(new(request.Dataset,current.GenerationId,request.ValueDate,DatabentoDatasetFailureReason.FullPipelineUnhealthy,
                TimeSpan.FromMinutes(2),TimeSpan.FromMinutes(2),request.OperationId),bounded.Token).ConfigureAwait(false);
            var next=current with{GenerationId=result.GenerationId,Healthy=result.Succeeded,Lifecycle=result.Succeeded?"Healthy":"StoppedError",Error=result.Succeeded?null:result.Detail,OperationId=request.OperationId,ChangedAtUtc=clock.GetUtcNow()};
            state.RecoveryChanged(next);
            return Remember(request,new(request.OperationId,current.Role,current.Dataset,result.Succeeded,result.Succeeded,result.Succeeded,result.Succeeded?null:result.Detail,next.WorkerInstanceId,next.GenerationId));
        }
        var scopes=epoch.GetOptionScopeStates();
        if(scopes.Values.Any(x=>x.Phase==5))
        {
            // The Rust lane already consumed its original two-minute incident.
            // Waiting another two minutes here would renew that failed recovery.
            var failed=current with{Healthy=false,Lifecycle="StoppedError",OperationId=request.OperationId,Error="Native option recovery exhausted its original two-minute deadline.",ChangedAtUtc=clock.GetUtcNow(),RecoveryDeadlineUtc=null};
            epoch.SignalOptionOwnerStop();
            state.RecoveryChanged(failed);
            return Remember(request,new(request.OperationId,current.Role,current.Dataset,false,false,false,failed.Error,current.WorkerInstanceId,current.GenerationId));
        }
        var through=clock.GetUtcNow();
        var receiverFailures=new Dictionary<string,Guid>(state.CaptureReceiverFailures());var deliveryRepaired=false;
        var replaced=false;var adopted=false;Guid replacementOwner=Guid.Empty;
        IReadOnlyList<TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainRequest>? restoredLeases=null;
        MarketDataRecoveryResult recovered;
        try{recovered=await episodes.RecoverAsync(current,request,async(remaining,ct)=>
        {
            var stepStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            TimeSpan Left()=>TimeSpan.FromTicks(Math.Max(0,remaining.Ticks-System.Diagnostics.Stopwatch.GetElapsedTime(stepStarted).Ticks));
            var failedReceiver=state.CaptureReceiverFailures();
            if(await epoch.EnsureDeliveryOperationalAsync(Left(),ct).ConfigureAwait(false))
            {
                deliveryRepaired=true;replaced=false;adopted=false;
                foreach(var item in failedReceiver)receiverFailures[item.Key]=item.Value;
            }
            if(!replaced)
            {
                restoredLeases=await epoch.ReplaceOptionOwnerAsync(application.RequalifyOptionsAsync,Left(),ct).ConfigureAwait(false);
                // Never repeat destructive replacement merely because an
                // application restore/adoption step needs another attempt.
                replaced=true;

            }
            if(!adopted){await application.AdoptRestoredOptionsAsync(restoredLeases!,ct,()=>replacementOwner=state.CommitOptionReplacement(request.ValueDate,replacementOwner==Guid.Empty?current.WorkerInstanceId:replacementOwner,epoch.OptionGeneration)).ConfigureAwait(false);adopted=true;}
            var saved=await application.ReadTradeFeedRecoveryAsync(request.ValueDate,ct).ConfigureAwait(false);
            foreach(var trade in saved)
                if(!await application.LoadTradeFeedRecoveryAsync(trade.TradeId,request.ValueDate,ct).ConfigureAwait(false))
                    throw new InvalidOperationException("Saved trade live-feed intent could not be restored: "+trade.TradeId);
            var actual=epoch.MonitoredOptionContracts.ToHashSet(StringComparer.Ordinal);
            if(saved.SelectMany(x=>x.OptionContracts).Any(x=>!actual.Contains(x)))return false;
            return !epoch.IsOptionOwnerReplacementPending&&epoch.GetOptionScopeStates().Values.All(x=>x.Phase==3);
        },token,()=>current with{WorkerInstanceId=replacementOwner==Guid.Empty?current.WorkerInstanceId:replacementOwner,GenerationId=epoch.OptionGeneration}).ConfigureAwait(false);}
        catch{epoch.SignalOptionOwnerStop();throw;}
        if(!recovered.Recovered){epoch.SignalOptionOwnerStop();recovered=recovered with{CleanupConfirmed=false};}
        if(recovered.Recovered){state.ProcessingRecovered(request.ValueDate,current.GenerationId,through);if(deliveryRepaired)state.DeliveryRecovered(receiverFailures);}
        return Remember(request,recovered);
    }
}
