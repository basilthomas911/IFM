using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>One bounded recovery episode per logical dataset role. Failed steps
/// retry within the original budget. A stuck callback stays owned and cannot
/// spawn more attempts; deadline expiry always publishes a terminal observation.</summary>
public sealed class NativeHostRecovery(TimeProvider clock,Action<MarketDataDatasetStatus> changed)
{
    readonly object gate=new();
    readonly Dictionary<string,Episode> active=new(StringComparer.Ordinal);
    sealed record Episode(Guid Id,DateOnly ValueDate,Task<MarketDataRecoveryResult> Completion);
    /// <summary>Joins the owned incident even after its native generation has
    /// advanced but application restoration has not finished. A join never grants
    /// a new deadline or starts another attempt.</summary>
    public bool TryJoin(MarketDataRecoveryRequest request,out Task<MarketDataRecoveryResult>? completion)
    {
        lock(gate)
        {
            if(active.TryGetValue(request.Role+"|"+request.Dataset,out var episode)&&episode.ValueDate==request.ValueDate)
            {completion=episode.Completion;return true;}
            completion=null;return false;
        }
    }
    /// <summary>Joins an existing episode or starts one capped at 120 seconds.
    /// Recovery attempts must prove transport, delivery and application readiness.
    /// The notification observer must be nonblocking and must retain terminal status.</summary>
    public Task<MarketDataRecoveryResult> RecoverAsync(MarketDataDatasetStatus current,MarketDataRecoveryRequest request,
        Func<TimeSpan,CancellationToken,Task<bool>> attempt,CancellationToken stoppingToken=default,Func<MarketDataDatasetStatus>? restoredStatus=null)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if(request.ValueDate!=current.ValueDate||request.Role!=current.Role||request.Dataset!=current.Dataset)
            throw new ArgumentException("Recovery identity differs from the active API session.",nameof(request));
        if(request.ExpectedWorkerInstanceId!=current.WorkerInstanceId||request.ExpectedGenerationId!=current.GenerationId)
            return Task.FromResult(new MarketDataRecoveryResult(request.OperationId,current.Role,current.Dataset,current.Healthy,true,current.Running,"A newer native owner is active; stale recovery ignored.",current.WorkerInstanceId,current.GenerationId));
        var key=current.Role+"|"+current.Dataset;
        lock(gate)
        {
            if(active.TryGetValue(key,out var existing))return existing.Completion;
            if(active.Count>=3)throw new InvalidOperationException("Native recovery role capacity exhausted.");
            var completion=new TaskCompletionSource<MarketDataRecoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            active.Add(key,new(request.OperationId,request.ValueDate,completion.Task));
            _=RunAsync(key,current,request,attempt,completion,stoppingToken,restoredStatus);
            return completion.Task;
        }
    }
    async Task RunAsync(string key,MarketDataDatasetStatus current,MarketDataRecoveryRequest request,
        Func<TimeSpan,CancellationToken,Task<bool>> attempt,TaskCompletionSource<MarketDataRecoveryResult> completion,CancellationToken stoppingToken,Func<MarketDataDatasetStatus>? restoredStatus)
    {
        var start=Stopwatch.GetTimestamp();
        var budget=request.DeadlineUtc is{} until?until-clock.GetUtcNow():TimeSpan.FromMinutes(2);
        budget=budget<TimeSpan.Zero?TimeSpan.Zero:budget>TimeSpan.FromMinutes(2)?TimeSpan.FromMinutes(2):budget;
        TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,budget.Ticks-Stopwatch.GetElapsedTime(start).Ticks));
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);deadline.CancelAfter(budget);
        Exception? failure=null;Task<bool>? pending=null;
        try
        {
            changed(current with{Healthy=false,Lifecycle="Recovering",OperationId=request.OperationId,ChangedAtUtc=clock.GetUtcNow(),RecoveryDeadlineUtc=clock.GetUtcNow()+budget,Error=request.Reason});
            while(Remaining()>TimeSpan.Zero)
            {
                deadline.Token.ThrowIfCancellationRequested();
                try
                {
                    pending=attempt(Remaining(),deadline.Token);
                    if(await pending.WaitAsync(deadline.Token).ConfigureAwait(false))
                    {
                        var restored=restoredStatus?.Invoke()??current;
                        changed(restored with{Healthy=true,Lifecycle="Healthy",OperationId=request.OperationId,ChangedAtUtc=clock.GetUtcNow(),RecoveryDeadlineUtc=null,Error=null});
                        completion.TrySetResult(new(request.OperationId,current.Role,current.Dataset,true,true,true,null,restored.WorkerInstanceId,restored.GenerationId));return;
                    }
                    failure=new InvalidOperationException("Native recovery has not proved end-to-end readiness.");
                }
                catch(Exception error)when(!deadline.IsCancellationRequested){failure=error;}
                // A failed step does not end or renew the episode. Every retry
                // gets only what remains of the one original monotonic budget.
                await Task.Delay(TimeSpan.FromMilliseconds(200),deadline.Token).ConfigureAwait(false);
            }
            throw new TimeoutException("The two-minute recovery deadline expired.",failure);
        }
        catch(Exception error)
        {
            if(pending is{IsCompleted:false})_=pending.ContinueWith(task=>_=task.Exception,CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
            if(stoppingToken.IsCancellationRequested){completion.TrySetCanceled(stoppingToken);return;}
            var detail="The two-minute recovery deadline expired. "+(failure??error).Message;
            try{changed((restoredStatus?.Invoke()??current) with{Healthy=false,Lifecycle="StoppedError",OperationId=request.OperationId,ChangedAtUtc=clock.GetUtcNow(),RecoveryDeadlineUtc=null,Error=detail});}
            catch(Exception notification){completion.TrySetException(new AggregateException("Recovery failure could not be retained for API/UI observation.",error,notification));return;}
            completion.TrySetResult(new(request.OperationId,current.Role,current.Dataset,false,pending?.IsCompleted!=false,false,detail,current.WorkerInstanceId,current.GenerationId));
        }
        finally
        {
            // Retain a non-cooperative attempt's ownership even after notifying
            // failure. It cannot launch another callback on a later request.
            if(pending is{IsCompleted:false} owned)
                _=owned.ContinueWith(task=>
                {
                    _=task.Exception;
                    lock(gate)if(active.TryGetValue(key,out var held)&&held.Id==request.OperationId)active.Remove(key);
                },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            else lock(gate)active.Remove(key);
        }
    }
}
