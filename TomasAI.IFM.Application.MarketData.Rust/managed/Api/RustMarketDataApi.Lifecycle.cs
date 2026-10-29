using System.Diagnostics;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;
public sealed partial class RustMarketDataApi
{
    /// <inheritdoc/>
    public async Task StartAsync(DateOnly valueDate,Func<Guid,int,string,Task>? errorMessageHandler=null,CancellationToken cancellationToken=default)
    {
        if(datasetOwner){await application.StartAsync(valueDate,errorMessageHandler,cancellationToken).ConfigureAwait(false);return;}
        await apiLifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if(lifecycle is not null&&application.GetRuntimeStatus().ActiveValueDate is null)await lifecycle.StartingAsync(valueDate,cancellationToken).ConfigureAwait(false);
            await application.StartAsync(valueDate,errorMessageHandler,cancellationToken).ConfigureAwait(false);
            if(lifecycle is not null)await lifecycle.StartedAsync(valueDate,cancellationToken).ConfigureAwait(false);
        }
        finally{apiLifecycle.Release();}
    }
    /// <inheritdoc/>
    public async Task StopAsync(DateOnly valueDate)
    {
        if(datasetOwner){await application.StopAsync(valueDate).ConfigureAwait(false);return;}
        var started=Stopwatch.GetTimestamp();using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,TimeSpan.FromMinutes(2).Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        await apiLifecycle.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            var failures=new List<Exception>();
            if(lifecycle is not null)try{await lifecycle.StoppingAsync(valueDate,deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
            // Fix the native owner's stop budget before the application policy
            // invokes its idempotent stop. No inner two-minute allowance is renewed.
            Task? nativeStopping=null;
            if(epochs is NativeMarketDataEpochFactory native&&native.TryGetCurrent(out var epoch)&&epoch!.ValueDate==valueDate)
                nativeStopping=epoch.StopAndDisposeAsync(Remaining()).AsTask();
            var stopping=application.StopAsync(valueDate);
            foreach(var pending in new[]{nativeStopping,stopping})if(pending is not null)
                try{await pending.WaitAsync(Remaining()).ConfigureAwait(false);}catch(Exception error)
                {failures.Add(error);if(!pending.IsCompleted)_=pending.ContinueWith(task=>_=task.Exception,CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);}
            if(failures.Count!=0)throw new AggregateException("Native API session shutdown failed within its original deadline.",failures);
        }
        finally{apiLifecycle.Release();}
    }
}
