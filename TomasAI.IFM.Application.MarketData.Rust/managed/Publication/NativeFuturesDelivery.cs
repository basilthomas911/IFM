using TomasAI.IFM.Application.MarketData.Rust.Delivery;

namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Primary-lane handler for the single runtime receiver. Reuses one
/// sixteen-record transfer array and drains immediately on native signals.
/// Source arrays contain values only; no native pointer survives an await.</summary>
public sealed class NativeFuturesDelivery(NativeMarketDataRuntime runtime,NativeFuturesPublication publication)
{
    readonly LivePublication[] records=new LivePublication[16];
    readonly SemaphoreSlim gate=new(1);
    int next,count;
    /// <summary>Serializes replacement against actual publication callbacks.
    /// Gate admission and native reconnection share one original remaining
    /// budget. Old pending records/buffers for this lane are discarded; other
    /// lanes retain their queued tail. An unjoined publisher causes a bounded
    /// error for the supervisor, never an unbounded gate wait.</summary>
    public async Task<LiveLaneStatus> ResetAsync(MarketDataLane lane,Guid expectedGeneration,TimeSpan remainingBudget,CancellationToken token=default,Action? qualified=null)
    {
        if(lane is not(MarketDataLane.FuturesGlbx or MarketDataLane.FuturesXcbf))throw new ArgumentOutOfRangeException(nameof(lane));
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        var started=System.Diagnostics.Stopwatch.GetTimestamp();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(remainingBudget);
        await gate.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            var status=runtime.ReadLiveStatus(lane);if(status.Generation!=expectedGeneration)return status;
            publication.RetireLane(lane);
            var kept=0;for(var index=next;index<count;index++)if(records[index].Source.Lane!=lane)records[kept++]=records[index];next=0;count=kept;
            var remaining=remainingBudget-System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if(remaining<=TimeSpan.Zero)throw new TimeoutException("Original native reset deadline expired before replacement.");
            await runtime.ResetLiveLaneAsync(lane,remaining,deadline.Token).ConfigureAwait(false);
            qualified?.Invoke();
            return runtime.ReadLiveStatus(lane);
        }
        finally{gate.Release();}
    }
    /// <summary>Invoked by NativeMarketDataDelivery's owned primary callback.
    /// Its token carries the original deadline. Errors escape to the mandatory
    /// recovery observer; no new timeout, retry task or polling timer is started.
    /// A failed record is reported, not silently retried with a new event identity;
    /// the retained tail is subsequently fenced against physical replacement.</summary>
    public async ValueTask OnChangesAsync(uint bits,CancellationToken cancellationToken)
    {
        if((bits&~6u)!=0)throw new ArgumentOutOfRangeException(nameof(bits));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var processed=0;
            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(next==count){count=runtime.DrainLivePublications(records);next=0;if(count==0)return;}
                var frame=records[next++];
                if(frame.Source.Lane is not(MarketDataLane.FuturesGlbx or MarketDataLane.FuturesXcbf))throw new InvalidDataException("Primary runtime emitted an unowned options lane.");
                await publication.ProcessAsync(frame,cancellationToken).ConfigureAwait(false);
                if(++processed==256){processed=0;await Task.Yield();}
            }
        }
        finally{gate.Release();}
    }
}
