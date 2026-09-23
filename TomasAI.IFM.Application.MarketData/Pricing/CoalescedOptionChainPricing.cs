using System.Collections.Concurrent;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Framework.MarketData.Pricing;
using Unified = TomasAI.IFM.Framework.OptionPricer.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record ChainIvObservation(double Volatility, OptionPricingContext Context, OptionPricingQuote Underlying,
    OptionPricingQuote Option, DateTimeOffset CalculatedAtUtc, string PolicyVersion);
public sealed record ChainSelectionCalculation(OptionPricingContext Context, OptionPricingQuote Underlying, OptionPricingQuote Option,
    ChainIvObservation? Iv, double? TheoreticalPrice, double? Delta, DateTimeOffset CalculatedAtUtc, OptionPricingFailure? Failure);

/// <summary>Bounded latest-state ingress with independent selection/risk workers. No calculation or I/O on quote ingress.</summary>
public sealed class CoalescedOptionChainPricing : IOptionChainGreeksEnricher, IRetainedOptionTradeEnricher, IAsyncDisposable
{
    readonly OptionChainPricingInputStore inputs;
    readonly Guid generation;
    readonly OptionPricingRefreshPolicy policy;
    readonly Func<string, OptionPricingQuote?> readUnderlying;
    readonly TimeProvider clock;
    readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    readonly object registration = new();
    readonly object selectionPass = new(), riskPass = new();
    int disposed;
    readonly CancellationTokenSource stopping = new();
    readonly Task selectionLoop;
    readonly Task riskLoop;
    readonly Action<string>? fault;
    readonly IOptionTradeEvidenceWriter? tradeEvidence;
    int selectionCursor, riskCursor;
    long observations, calculations, ivSolves;
    public long QuoteObservations => Interlocked.Read(ref observations);
    public long SelectionCalculations => Interlocked.Read(ref calculations);
    public long IvSolves => Interlocked.Read(ref ivSolves);
    public void Register(DatabentoOptionChainRoute route)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        lock (registration)
        {
            if (entries.ContainsKey(route.FuturesOptionContractId)) return;
            if (entries.Count >= policy.MaximumContracts) throw new InvalidOperationException("Option pricing capacity exceeded.");
            entries[route.FuturesOptionContractId] = new(route);
        }
    }

    public CoalescedOptionChainPricing(OptionChainPricingInputStore inputs, Guid generation, OptionPricingRefreshPolicy policy,
        Func<string, OptionPricingQuote?> readUnderlying, TimeProvider? time = null, Action<string>? fault = null,
        IOptionTradeEvidenceWriter? tradeEvidence = null)
    {
        policy.Validate();
        this.inputs = inputs; this.generation = generation; this.policy = policy;
        this.readUnderlying = readUnderlying; clock = time ?? TimeProvider.System; this.fault = fault;
        this.tradeEvidence = tradeEvidence;
        selectionLoop = Task.Run(() => LoopAsync(false));
        riskLoop = Task.Run(() => LoopAsync(true));
    }
    public OptionGreeksSnapshot EnrichQuote(DatabentoOptionChainRoute route, LastQuoteTickSnapshot tick)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (!inputs.TryGet(route.FuturesOptionContractId, out var input) || input is null || input.Context.GenerationId != generation)
            return Pending(route, "PricingContextUnavailable");
        Entry entry;
        lock (registration)
        {
            if (!entries.TryGetValue(route.FuturesOptionContractId, out entry!))
            {
                if (entries.Count >= policy.MaximumContracts) return Pending(route, "PricingCapacity");
                entry = new(route); entries[route.FuturesOptionContractId] = entry;
            }
        }
        lock (entry.Gate)
        {
            if (entry.Latest is { } prior && (tick.EventTimestamp < prior.EventTimestamp
                || tick.EventTimestamp == prior.EventTimestamp && tick.SourceSequence <= prior.SourceSequence))
                return Pending(route, "SupersededQuote");
            entry.Latest = tick with { LocalReceivedAtUtc = clock.GetUtcNow() };
        }
        inputs.ObserveQuote(route.FuturesOptionContractId, input, tick);
        Interlocked.Increment(ref observations);
        return Pending(route, "CalculationPending");
    }
    // Every trade has its own calculation, independent of the coalesced quote/IV cache.
    public OptionGreeksSnapshot EnrichTrade(DatabentoOptionChainRoute route, LastTradeTickSnapshot tick)
    {
        if (!inputs.TryGet(route.FuturesOptionContractId, out var input) || input is null
            || input.Context.GenerationId != generation
            || route.Definition.Instrument.InstrumentId != input.Context.Contract.InstrumentId
            || route.Definition.Instrument.PublisherId != input.Context.Contract.PublisherId
            || route.Definition.Dataset != input.Context.Contract.Dataset
            || route.Definition.RawSymbol != input.Context.Contract.RawSymbol)
            return Pending(route, "PricingContextUnavailable") with { PriceSource = OptionGreeksPriceSource.Trade };
        return OptionTradePricing.Calculate(input.Context, readUnderlying(input.Context.Contract.UnderlyingContractId),
            tick, route.Definition.StrikePrice, route.Definition.Right == OptionRightSelection.Call, clock.GetUtcNow());
    }

    public ChainSelectionCalculation? ReadSelection(string id)
    {
        if (!entries.TryGetValue(id, out var entry)) return null;
        var value = Volatile.Read(ref entry.Selection);
        if (value is null) return null;
        var at = clock.GetUtcNow();
        if (!inputs.TryGet(id, out var current) || current?.Context != value.Context
            || Black76PricingModel.ValidateInputs(value.Context, value.Underlying, value.Option,
                entry.Route.Definition.StrikePrice, entry.Route.Definition.Right == OptionRightSelection.Call, at) is not null
            || value.Iv is { } iv && (at - iv.Option.EventAtUtc).TotalMilliseconds > policy.ImpliedVolatilityMilliseconds)
            return value with { TheoreticalPrice = null, Delta = null, Failure = Failure(id, "StaleData") };
        return value;
    }
    public async ValueTask<OptionGreeksSnapshot> EnrichTradeAsync(DatabentoOptionChainRoute route,
        LastTradeTickSnapshot tick, long eventNanoseconds, long receiveNanoseconds, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow();
        inputs.TryGet(route.FuturesOptionContractId, out var input);
        var context = input?.Context;
        if (context is not null && (context.GenerationId != generation
            || context.Contract.Dataset != route.Definition.Dataset
            || context.Contract.PublisherId != route.Definition.Instrument.PublisherId
            || context.Contract.InstrumentId != route.Definition.Instrument.InstrumentId
            || context.Contract.RawSymbol != route.Definition.RawSymbol
            || context.Contract.ContractId != tick.ContractId)) context = null;
        var underlying = context is null ? null : readUnderlying(context.Contract.UnderlyingContractId);
        OptionGreeksSnapshot result;
        try
        {
            result = context is null ? Pending(route, "PricingContextUnavailable") with { PriceSource = OptionGreeksPriceSource.Trade }
                : OptionTradePricing.Calculate(context, underlying, tick, route.Definition.StrikePrice,
                    route.Definition.Right == OptionRightSelection.Call, at);
        }
        catch (Exception error) when (error is ArgumentException or ArithmeticException)
        {
            result = Pending(route, "TradePricingFailed") with { PriceSource = OptionGreeksPriceSource.Trade };
        }
        var evidence = new OptionTradeEvidence(new(route.Definition.Dataset, route.Definition.Instrument.PublisherId,
            route.Definition.Instrument.InstrumentId, tick.ContractId, tick.ValueDate, tick.Price, tick.Size,
            tick.SourceSequence, eventNanoseconds, receiveNanoseconds, generation, route.Definition.RawSymbol), context, underlying, at,
            result.IsValid ? new(result.ImpliedVolatility!.Value, result.Delta!.Value, result.Gamma!.Value,
                result.Theta!.Value, result.Vega!.Value, result.Rho!.Value, result.TheoreticalPrice!.Value,
                result.TimeToExpiryYears!.Value, result.PricingContextDigest!) : null,
            result.IsValid ? null : result.PricingFailure ?? Failure(tick.ContractId, "TradePricingFailed"));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            if (tradeEvidence is null) throw new InvalidOperationException("No durable option-trade evidence writer is configured.");
            await tradeEvidence.WriteAsync(evidence, deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch
        {
            fault?.Invoke("Option trade evidence was not durably acknowledged; chain retention stopped.");
            throw;
        }
        return result;
    }
    public OptionPricingPassResult? ReadRisk(string id)
    {
        if (!entries.TryGetValue(id, out var entry)) return null;
        var risk = Volatile.Read(ref entry.Risk);
        if (risk is null) return null;
        if (!inputs.TryGet(id, out var current) || current?.Context != risk.Context
            || Black76PricingModel.ValidateInputs(risk.Context, risk.Underlying, risk.Option,
                entry.Route.Definition.StrikePrice, entry.Route.Definition.Right == OptionRightSelection.Call, clock.GetUtcNow()) is not null)
            return new(null, Failure(id, "StaleData"));
        return risk.Result;
    }
    public void RequireRisk(string id, bool required) { if (entries.TryGetValue(id, out var entry)) entry.RiskRequired = required; }
    public void Remove(string id) { lock (registration) entries.TryRemove(id, out _); }

    async Task LoopAsync(bool risk)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(risk ? policy.FullRiskMilliseconds : policy.PriceDeltaMilliseconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stopping.Token).ConfigureAwait(false))
                await ProcessSweepAsync(risk).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (Exception e) { fault?.Invoke("Option pricing scheduler failed: " + e.GetType().Name); throw; }
    }
    /// <summary>Deterministic pass for synthetic-clock verification; production is driven by the owned background loops.</summary>
    public void ProcessPass(bool risk = false)
    {
        lock (risk ? riskPass : selectionPass) ProcessPassCore(risk);
    }
    /// <summary>One coalesced sweep per cadence, yielding between bounded batches. Sweeps never queue timer ticks.</summary>
    public async Task ProcessSweepAsync(bool risk = false)
    {
        var batch = entries.Values.Where(x => !risk || x.RiskRequired).ToArray();
        for (var processed = 0; processed < batch.Length; processed += policy.MaximumContractsPerPass)
        {
            stopping.Token.ThrowIfCancellationRequested();
            lock (risk ? riskPass : selectionPass)
                ProcessPassCore(risk, batch, Math.Min(policy.MaximumContractsPerPass, batch.Length - processed));
            await Task.Yield();
        }
    }
    void ProcessPassCore(bool risk, Entry[]? batch = null, int? maximum = null)
    {
        batch ??= entries.Values.Where(x => !risk || x.RiskRequired).ToArray();
        if (batch.Length == 0) return;
        ref var cursor = ref (risk ? ref riskCursor : ref selectionCursor);
        for (var i = 0; i < Math.Min(maximum ?? policy.MaximumContractsPerPass, batch.Length); ++i)
        {
            stopping.Token.ThrowIfCancellationRequested();
            var entry = batch[cursor++ % batch.Length];
            if (cursor >= batch.Length) cursor = 0;
            if (risk && !entry.RiskRequired) continue;
            LastQuoteTickSnapshot? tick;
            lock (entry.Gate) tick = entry.Latest;
            if (tick is not { BidPrice: not null, AskPrice: not null } observed
                || !inputs.TryGet(entry.Route.FuturesOptionContractId, out var input) || input is null) continue;
            var context = input.Context;
            var underlying = readUnderlying(context.Contract.UnderlyingContractId);
            if (underlying is null)
            {
                Volatile.Write(ref entry.Selection, null);
                Volatile.Write(ref entry.Risk, null);
                continue;
            }
            var quote = new OptionPricingQuote(observed.ContractId, observed.BidPrice!.Value, observed.AskPrice!.Value,
                observed.BidSize, observed.AskSize, observed.EventTimestamp,
                observed.LocalReceivedAtUtc == default ? observed.ReceiveTimestamp : observed.LocalReceivedAtUtc,
                observed.SourceSequence, generation);
            var at = clock.GetUtcNow();
            var strike = entry.Route.Definition.StrikePrice;
            var call = entry.Route.Definition.Right == OptionRightSelection.Call;
            var invalid = Black76PricingModel.ValidateInputs(context, underlying, quote, strike, call, at);
            if (context.GenerationId != generation || entry.Route.Definition.Instrument.InstrumentId != context.Contract.InstrumentId
                || entry.Route.Definition.Instrument.PublisherId != context.Contract.PublisherId
                || entry.Route.Definition.Dataset != context.Contract.Dataset || entry.Route.Definition.RawSymbol != context.Contract.RawSymbol)
                invalid = Failure(quote.ContractId, "Recovering");
            if (invalid is not null)
            {
                if (risk) Volatile.Write(ref entry.Risk, new(context, underlying, quote, new(null, invalid)));
                else Volatile.Write(ref entry.Selection, new(context, underlying, quote, null, null, null, at, invalid));
                continue;
            }
            if (risk)
            {
                var result = Black76PricingModel.Calculate(context, underlying, quote, strike, call, at);
                if (inputs.TryGet(quote.ContractId, out var latest) && latest?.Context == context)
                    Volatile.Write(ref entry.Risk, new(context, underlying, quote, result));
                continue;
            }
            var calculator = new Unified.OptionCalculator();
            var request = Black76PricingModel.CreateRequest(context.Contract, (double)(underlying.Bid / 2 + underlying.Ask / 2),
                strike, call, OptionPricingQualification.YearFraction(context.Contract, at), context.Rate.AnnualContinuousRate);
            var iv = entry.Iv;
            if (iv is null || iv.Context != context || iv.PolicyVersion != policy.Version
                || (at - iv.Option.EventAtUtc).TotalMilliseconds >= policy.ImpliedVolatilityMilliseconds)
            {
                Interlocked.Increment(ref ivSolves);
                var solved = calculator.SolveImpliedVolatility(request, (double)(quote.Bid / 2 + quote.Ask / 2), stopping.Token);
                if (!solved.Success)
                {
                    Volatile.Write(ref entry.Selection, new(context, underlying, quote, null, null, null, at,
                        Failure(quote.ContractId, solved.Failure.ToString())));
                    continue;
                }
                iv = new(solved.Value!.Value.Volatility, context, underlying, quote, at, policy.Version);
                entry.Iv = iv;
            }
            var priced = calculator.PriceAndDelta(request, iv.Volatility, stopping.Token);
            Interlocked.Increment(ref calculations);
            if (!inputs.TryGet(quote.ContractId, out var fresh) || fresh?.Context != context) continue;
            Volatile.Write(ref entry.Selection, new(context, underlying, quote, iv, priced.Value?.Price, priced.Value?.Delta, at,
                priced.Success ? null : Failure(quote.ContractId, priced.Failure.ToString())));
        }
    }
    OptionGreeksSnapshot Pending(DatabentoOptionChainRoute route, string code) => new()
    {
        IsValid = false, FuturesContractId = route.Definition.Underlying, CalculatedAtUtc = clock.GetUtcNow(),
        FailureReason = OptionGreeksFailureReason.PricingContextUnavailable, PricingFailure = Failure(route.FuturesOptionContractId, code)
    };
    static OptionPricingFailure Failure(string id, string code) => new(code, "OptionPricing", id, "Qualified calculation is unavailable.");
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await stopping.CancelAsync().ConfigureAwait(false);
        try { await Task.WhenAll(selectionLoop, riskLoop).ConfigureAwait(false); }
        finally
        {
            lock (selectionPass) lock (riskPass) lock (registration) entries.Clear();
            stopping.Dispose();
        }
    }
    sealed record RiskCalculation(OptionPricingContext Context, OptionPricingQuote Underlying,
        OptionPricingQuote Option, OptionPricingPassResult Result);
    sealed class Entry(DatabentoOptionChainRoute route)
    {
        public readonly object Gate = new();
        public DatabentoOptionChainRoute Route { get; } = route;
        public LastQuoteTickSnapshot? Latest;
        public ChainIvObservation? Iv;
        public ChainSelectionCalculation? Selection;
        public RiskCalculation? Risk;
        public volatile bool RiskRequired;
    }
}
