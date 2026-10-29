using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Ordered actual-trade processing, independent of latest quote views.
/// Each source record is priced with frozen inputs, durably acknowledged, then
/// mapped through the current application event contract. One failed callback
/// cannot spawn an unbounded succession of detached persistence/publisher tasks.</summary>
public sealed class NativeOptionTradeDelivery(NativeOptionTradeSourceReader source, RustOptionTradePricing pricing,
    NativeOptionChainRuntime scopes, IOptionTradeEvidenceWriter evidence, ITickAggregationEventPublisher publisher,ITickLiveRouter? liveRouter=null)
{
    readonly SemaphoreSlim pass = new(1);
    readonly NativeCapturedOptionTrade?[] transfer = new NativeCapturedOptionTrade?[1];
    readonly LivePublication[] raw=new LivePublication[1];
    Task? inFlight;
    Acknowledged? activeWriter;
    internal async Task ExclusiveAsync(Func<Task> action,CancellationToken token)
    {
        await pass.WaitAsync(token).ConfigureAwait(false);
        try
        {
            foreach(var pending in new[]{inFlight,activeWriter?.Outstanding})
                if(pending is not null)try{await pending.WaitAsync(token).ConfigureAwait(false);}catch when(pending.IsCompleted){_=pending.Exception;}
            await action().ConfigureAwait(false);
        }
        finally{pass.Release();}
    }
    internal async Task JoinAsync(CancellationToken token)
    {
        await pass.WaitAsync(token).ConfigureAwait(false);
        try
        {
            foreach(var pending in new[]{inFlight,activeWriter?.Outstanding})
                if(pending is not null)try{await pending.WaitAsync(token).ConfigureAwait(false);}catch when(pending.IsCompleted){_=pending.Exception;}
        }
        finally { pass.Release(); }
    }

    sealed class Acknowledged(IOptionTradeEvidenceWriter writer) : IOptionTradeEvidenceWriter
    {
        internal OptionTradeEvidence? Value;
        internal Task? Outstanding;
        public async ValueTask WriteAsync(OptionTradeEvidence value, CancellationToken token)
        {
            var pending = writer.WriteAsync(value, token).AsTask(); Volatile.Write(ref Outstanding, pending);
            await pending.ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); Value = value;
        }
    }

    async Task PublishAsync(string scope, NativeCapturedOptionTrade trade,LivePublication source, TimeSpan remaining, CancellationToken token)
    {
        var acknowledged = new Acknowledged(evidence);
        activeWriter = acknowledged;
        var greeks = await pricing.CalculateAndRetainAsync(trade, acknowledged, remaining, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var retained = acknowledged.Value ?? throw new InvalidOperationException("Option source evidence was not durably acknowledged.");
        var tick = new LastTradeTickSnapshot(trade.Source.ContractId, trade.Source.ValueDate, trade.Source.Price, trade.Source.Size,
            trade.Source.Sequence, DateTimeOffset.UnixEpoch.AddTicks(trade.Source.EventNanoseconds / 100),
            DateTimeOffset.UnixEpoch.AddTicks(trade.Source.ReceiveNanoseconds / 100));
        foreach (var binding in scopes.MonitoredQuotes)
            if (binding.ScopeId == scope && binding.Prepared.Context.Contract.ContractId == trade.Source.ContractId && binding.IsCurrent())
                binding.Reader.RetainAcknowledgedTrade(trade.PhysicalGeneration, trade.ContextIdentity, new(tick, greeks));
        var route=scopes.FindMonitoredQuote(scope,source.Source.Instrument);
        if(route is not null&&route.IsCurrent()&&liveRouter?.IsActive(trade.Source.ContractId)==true)
            await liveRouter.RouteAsync(new LiveTickTradeServiceEvent(Guid.NewGuid(),trade.Source.ContractId,trade.Source.ValueDate,
                AssetTypeId.FuturesOption,trade.Source.Dataset,trade.Source.ValueDate,
                trade.Source.PublisherId,trade.Source.InstrumentId,NativeWireEvents.Trade(source.Source)),token).ConfigureAwait(false);
        // This is the existing idempotent event mapping, including exact native
        // nanoseconds. It is called only after persistence acknowledgment.
        await publisher.PublishAsync(retained.ToRealtimeEvent(), token).ConfigureAwait(false);
    }

    /// <summary>Consumes up to 256 immediately available source records under
    /// one original caller budget. A return of 256 means the caller should invoke
    /// again without waiting for another signal. No source batch is popped ahead
    /// of a write that can fail; at most one trade is owned by the callback.</summary>
    public async Task<int> DrainAvailableAsync(string scope, TimeSpan remainingBudget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(scopes); ArgumentNullException.ThrowIfNull(evidence); ArgumentNullException.ThrowIfNull(publisher);
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if (remainingBudget > TimeSpan.FromMinutes(2)) remainingBudget = TimeSpan.FromMinutes(2);
        var started = Stopwatch.GetTimestamp();
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); bounded.CancelAfter(remainingBudget);
        await pass.WaitAsync(bounded.Token).ConfigureAwait(false);
        try
        {
            if (inFlight is { IsCompleted: false } || activeWriter is { } previousWriter && Volatile.Read(ref previousWriter.Outstanding) is { IsCompleted: false })
                throw new InvalidOperationException("Previous option trade callback has not joined; its application owner still requires recovery.");
            _ = activeWriter?.Outstanding?.Exception; activeWriter = null;
            _ = inFlight?.Exception; inFlight = null;
            var total = 0;
            while (total < 256)
            {
                bounded.Token.ThrowIfCancellationRequested();
                var trades = source.Drain(scope, transfer,raw,out var consumed);
                if (consumed == 0) return total;
                total += consumed;
                var remaining = remainingBudget - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero) throw new TimeoutException("Original option trade delivery deadline expired.");
                if(trades==0)
                {
                    var frame=raw[0];var route=scopes.FindMonitoredQuote(scope,frame.Source.Instrument);
                    if(frame.Source.Kind!=1||route is null||!route.IsCurrent()||liveRouter?.IsActive(route.Prepared.Context.Contract.ContractId)!=true)continue;
                    var c=route.Prepared.Context.Contract;
                    inFlight=liveRouter.RouteAsync(new LiveTickQuoteServiceEvent(Guid.NewGuid(),c.ContractId,route.Reader.ValueDate,
                        AssetTypeId.FuturesOption,c.Dataset,route.Reader.ValueDate,c.PublisherId,c.InstrumentId,
                        NativeWireEvents.Quote(frame.Source)),bounded.Token).AsTask();
                }
                else{var captured=transfer[0]!;transfer[0]=null;inFlight=PublishAsync(scope,captured,raw[0],remaining,bounded.Token);}
                await inFlight.WaitAsync(bounded.Token).ConfigureAwait(false);
                if (Stopwatch.GetElapsedTime(started) >= remainingBudget) throw new TimeoutException("Original option trade delivery deadline expired.");
                inFlight = null;
            }
            return total;
        }
        finally { pass.Release(); }
    }
}
