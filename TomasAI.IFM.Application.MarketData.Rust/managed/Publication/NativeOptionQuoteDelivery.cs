using System.Collections.Immutable;
using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Publishes current monitoring views independently from the ordered
/// trade/evidence drain. Invoke on native options signals: this class neither
/// polls nor waits for a quote batch to fill. One generation-wide publisher
/// preserves actor revision ordering through physical resets/context refreshes.</summary>
public sealed class NativeOptionQuoteDelivery(NativeOptionChainRuntime scopes,NativeOptionObservationPublisher publisher)
{
    readonly SemaphoreSlim pass=new(1);
    readonly Dictionary<string,Published> latest=new(StringComparer.Ordinal);
    ImmutableArray<NativeMonitoredOptionQuote> directory;
    Task? publicationInFlight;
    readonly record struct Published(Guid Physical,Guid Context,LastQuoteTickWithGreeksSnapshot Value,bool Pricing);
    internal async Task ExclusiveAsync(Func<Task> action,CancellationToken token)
    {
        await pass.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if(publicationInFlight is{} pending)try{await pending.WaitAsync(token).ConfigureAwait(false);}catch when(pending.IsCompleted){_=pending.Exception;}
            await action().ConfigureAwait(false);
        }
        finally{pass.Release();}
    }
    internal async Task JoinAsync(CancellationToken token)
    {
        await pass.WaitAsync(token).ConfigureAwait(false);
        try { if(publicationInFlight is { } pending)try{await pending.WaitAsync(token).ConfigureAwait(false);}catch when(pending.IsCompleted){_=pending.Exception;} }
        finally { pass.Release(); }
    }
    /// <summary>Reads already-available latest views and awaits publisher
    /// acceptance under one remaining budget. Unexpected reader/publisher errors
    /// escape to the owning recovery observer. No quote source or lease is acquired.
    /// The count measures accepted candidates; existing unchanged-mark suppression
    /// can intentionally emit fewer actor packets.</summary>
    public async Task<int> PublishAvailableAsync(TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(scopes);ArgumentNullException.ThrowIfNull(publisher);
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        var started=Stopwatch.GetTimestamp();
        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);bounded.CancelAfter(remainingBudget);
        await pass.WaitAsync(bounded.Token).ConfigureAwait(false);
        try
        {
            if(publicationInFlight is {IsCompleted:false})
                throw new InvalidOperationException("The previous option publisher has not completed cancellation; its application owner still requires recovery.");
            _=publicationInFlight?.Exception; // Observe a callback that completed after its caller deadline.
            publicationInFlight=null;
            var current=scopes.MonitoredQuotes;
            if(current!=directory)
            {
                var ids=current.Select(x=>x.Prepared.Context.Contract.ContractId).ToHashSet(StringComparer.Ordinal);
                foreach(var id in latest.Keys.Where(id=>!ids.Contains(id)).ToArray())latest.Remove(id);
                publisher.RetainContracts(ids);directory=current;
            }
            var count=0;
            foreach(var item in current)
            {
                bounded.Token.ThrowIfCancellationRequested();if(!item.IsCurrent())continue;
                var pricing=item.Reader.TryGetLastQuoteWithGreeks(out var value,out var physical);
                if(!pricing)
                {
                    if(!item.Reader.TryGetLastQuote(out var quote,out physical))continue;
                    value=new(quote,new(){FuturesContractId=item.Prepared.Context.Contract.UnderlyingContractId,
                        CalculatedAtUtc=quote.LocalReceivedAtUtc==default?quote.ReceiveTimestamp:quote.LocalReceivedAtUtc,
                        FailureReason=OptionGreeksFailureReason.PricingContextUnavailable,
                        PricingFailure=new("CalculationPending","OptionPricing",quote.ContractId,"Qualified calculation is unavailable.")});
                }
                var id=item.Prepared.Context.Contract.ContractId;var next=new Published(physical,item.Prepared.Identity,value,pricing);
                if(latest.TryGetValue(id,out var previous)&&previous==next||!item.IsCurrent())continue;
                var source=new FuturesOptionChainQuoteChangedServiceEvent(Guid.NewGuid(),item.Prepared.Context.Contract.UnderlyingContractId,id,
                    item.Reader.ValueDate,item.MaturityDate,value.Tick,value.Greeks);
                // A timed-out callback retains ownership of its managed packet;
                // no native pointer or pooled buffer is returned/reused here.
                // Retain at most one unjoined callback rather than detaching a
                // new task on every retry if a publisher violates cancellation.
                publicationInFlight=publisher.PublishQuoteAsync(source,item.Prepared.Context,pricing,bounded.Token).AsTask();
                await publicationInFlight.WaitAsync(bounded.Token).ConfigureAwait(false);
                if(Stopwatch.GetElapsedTime(started)>=remainingBudget)throw new TimeoutException("Native option observation original publication deadline expired.");
                publicationInFlight=null;
                latest[id]=next;count++;
            }
            return count;
        }
        finally{pass.Release();}
    }
}
