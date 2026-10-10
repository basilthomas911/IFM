using TomasAI.IFM.Application.MarketData.Pricing;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Captures the established trade, published risk policy and current OptionCalculator scenarios outside tick handling.</summary>
/// <remarks>One refresh per trade/session runs at a time. Missing providers stay unavailable; old fixture rows and fabricated
/// distributions are never substituted. This is a read cache, not command state or a trade-plan replay queue.</remarks>
public sealed class IronCondorMonitoringInputReader(IDbContextFactory databases, ILogger logger, IFinancialQueryStore? financialQueries = null, IndividualOptionRiskReader? optionRisk = null,
    Func<InitializeIronCondorMonitoringCommand, CancellationToken, Task>? initializeMonitoring = null,
    Func<InsertSpreadDistributionCommand, CancellationToken, Task>? recordDistributions = null,
    Func<TradeLegDefinition[], DateOnly, CancellationToken, Task<MarketCompositionSnapshot[]>>? captureOptionRisk = null,
    CancellationToken stoppingToken = default,
    Func<StrategyPositionSnapshot, DateOnly, IronCondorTradePlanInputs, DateTime, Task>? publishInputChange = null, StrategyRiskParameterSetResolver? riskParameterSets = null)
{
    int observerStarted;
    readonly ConcurrentDictionary<(TradeEntityId, DateOnly), Entry> entries = new();

    /// <summary>Returns captured references immediately and schedules bounded background refreshes when due.</summary>
    /// <param name="position">The coherent observed position and full trade identity.</param>
    /// <param name="valueDate">The active exchange session.</param>
    /// <param name="nowUtc">The captured evaluation instant.</param>
    /// <returns>Available observations, or null while the initial provider read is pending.</returns>
    public IronCondorTradePlanInputs? Capture(StrategyPositionSnapshot position, DateOnly valueDate, DateTime nowUtc)
    {
        _ = financialQueries; _ = initializeMonitoring; _ = recordDistributions; // Constructor compatibility only: retired providers are never invoked.
        stoppingToken.ThrowIfCancellationRequested();
        var entry = entries.GetOrAdd((position.Id.Trade, valueDate), _ => new());
        if (publishInputChange is not null && Interlocked.CompareExchange(ref observerStarted, 1, 0) == 0)
            _ = Task.Run(ObserveChangesAsync);
        lock (entry)
        {
            if (entry.Position is null || position.RouteGeneration > entry.Position.RouteGeneration
                || position.RouteGeneration == entry.Position.RouteGeneration && position.PositionSequence >= entry.Position.PositionSequence)
                entry.Position = position;
            if (!entry.Refreshing && nowUtc >= entry.NextRefreshUtc)
            {
                entry.Refreshing = true;
                entry.NextRefreshUtc = nowUtc.AddSeconds(5);
                _ = Task.Run(() => RefreshAsync(entry, position, valueDate, nowUtc));
            }
            if (!entry.PricingRefreshing && entry.Trade is not null && nowUtc >= entry.NextPricingUtc)
            {
                entry.PricingRefreshing = true;
                entry.NextPricingUtc = nowUtc.AddSeconds(1);
                _ = Task.Run(() => RefreshPricingAsync(entry, position, valueDate));
            }
            if (entry.Inputs is not { } inputs) return null;
            return inputs.OptionRiskValidUntilUtc is { } until && until <= nowUtc
                ? inputs with { ShortPutGamma = null, ShortCallGamma = null,
                    PutOTMProbability = null, CallOTMProbability = null, ForwardDelta = null, CalculatedSpreadPrices = null }
                : inputs;
        }
    }

    /// <summary>Checks current input changes and freshness once per second outside option tick handling.</summary>
    /// <returns>The generation-owned observer; no plans, profit series or replay state are retained.</returns>
    async Task ObserveChangesAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                var now = DateTime.UtcNow;
                if (!FuturesTradingValueDate.TryGet(new DateTimeOffset(now), out var date)) continue;
                foreach (var cached in entries.ToArray())
                {
                    StrategyPositionSnapshot? position; EstablishedTradeDefinition? trade;
                    lock (cached.Value) { position = cached.Value.Position; trade = cached.Value.Trade; }
                    if (position is null || !position.IsOpen) { entries.TryRemove(cached); continue; }
                    if (cached.Key.Item2 != date) { entries.TryRemove(cached); Capture(position, date, now); continue; }
                    var owned = trade is not null && (captureOptionRisk is not null
                        || optionRisk?.HasScopes(trade.Legs.Select(leg => leg.ContractId), date) == true);
                    IronCondorTradePlanInputs? inputs;
                    if (owned) inputs = Capture(position, date, now);
                    else
                    {
                        lock (cached.Value) inputs = cached.Value.Inputs;
                        if (inputs is not null) inputs = inputs with { CalculatedSpreadPrices = null, ShortPutGamma = null,
                            ShortCallGamma = null, ForwardDelta = null, PutOTMProbability = null, CallOTMProbability = null, DailyRiskInputs = null };
                    }
                    if (inputs is null) continue;
                    // A stale transition is part of this identity, so it is emitted once even without a new quote.
                    var stale = position.AsOfUtc == default || position.AsOfUtc > now || now-position.AsOfUtc > TimeSpan.FromSeconds(30);
                    var identity = new UpdateIronCondorTradePlanCommand { EntityId = new(position.Id, date), Position = position,
                        IronCondorTradePlanInputs = inputs }.Fingerprint() + (stale ? "|stale" : "|current");
                    lock (cached.Value)
                    {
                        if (identity == cached.Value.LastPublishedInputs) continue;
                        cached.Value.LastPublishedInputs = identity; // Disposable notification: no retries if publication fails.
                    }
                    stoppingToken.ThrowIfCancellationRequested();
                    try { await publishInputChange!(position, date, inputs, now).WaitAsync(stoppingToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        logger.LogError(error, "Monitoring input notification dropped; MethodName={MethodName} PositionId={PositionId} ValueDate={ValueDate}",
                            nameof(ObserveChangesAsync), position.Id.Format(), date);
                    }
                }

            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error) { logger.LogError(error, "Monitoring observer failed; MethodName={MethodName}", nameof(ObserveChangesAsync)); }
    }

    /// <summary>Reads matching persisted provider observations once per refresh and publishes an immutable read-cache entry.</summary>
    /// <param name="entry">The cache slot; no actor command state is mutated.</param>
    /// <param name="position">The exact established trade identity.</param>
    /// <param name="valueDate">The required exchange session.</param>
    /// <param name="nowUtc">The refresh observation time.</param>
    /// <returns>The bounded provider load operation.</returns>
    async Task RefreshAsync(Entry entry, StrategyPositionSnapshot position, DateOnly valueDate, DateTime nowUtc)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var token = timeout.Token;
            var trade = await databases.TradeDb.GetEstablishedTradeAsync(position.Id.Trade, token).WaitAsync(token).ConfigureAwait(false);
            if (trade is null || trade.Id != position.Id.Trade || trade.StrategyKind != TradeStrategyKind.IronCondor
                || trade.Legs.Length != 4) throw new InvalidOperationException("IronCondorMonitoring.TRADE.UNAVAILABLE: matching persisted trade is required.");
            var policy = riskParameterSets is null ? null : await riskParameterSets.ResolveAsync(token).ConfigureAwait(false);
            if (trade.MaturityDate is not { } expiry)
                throw new InvalidOperationException("IronCondorMonitoring.EXPIRY.INVALID: expiry required for every leg.");
            var inputs = new IronCondorTradePlanInputs
            {
                StrategyRiskParameterSet = policy,
                ContractCashMultiplier = trade.Legs.Select(leg => leg.CashMultiplier).Distinct().Count() == 1
                    && trade.Legs[0].CashMultiplier > 0 ? trade.Legs[0].CashMultiplier : null,
                OpeningCommission = trade.OpeningCommission, TradeType = IdentifyTradeType(trade.Legs),
                TradeDate = trade.TradeDate,
                MaturityDate = expiry
            };
            token.ThrowIfCancellationRequested();
            lock (entry)
            {
                // A reference load cannot roll back fresher calculator observations.
                if (entry.Inputs is { } current && current.CalculatedSpreadPrices is not null)
                    inputs = CopyPricing(inputs, current);
                entry.Inputs = inputs; entry.Trade = trade;
            }

        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogError(error, "Monitoring input capture failed; MethodName={MethodName} TradeId={TradeId} ValueDate={ValueDate}",
                nameof(RefreshAsync), position.Id.Trade.Format(), valueDate);
        }
        finally { lock (entry) entry.Refreshing = false; }
    }

    /// <summary>Refreshes qualified option prices without rereading database references or awaiting a history commit.</summary>
    /// <param name="entry">The bounded reference and pricing cache slot.</param><param name="position">The latest captured position.</param>
    /// <param name="valueDate">The current exchange session.</param><returns>The bounded four-leg valuation operation.</returns>
    async Task RefreshPricingAsync(Entry entry, StrategyPositionSnapshot position, DateOnly valueDate)
    {
        try
        {
            EstablishedTradeDefinition trade;
            lock (entry) trade = entry.Trade!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var token = timeout.Token;
            var risk = captureOptionRisk is not null
                ? await captureOptionRisk(trade.Legs, valueDate, token).WaitAsync(token).ConfigureAwait(false)
                : await ReadOptionRiskAsync(trade.Legs, valueDate, token).WaitAsync(token).ConfigureAwait(false);
            if (risk.Length != 4) return; // Current capture will invalidate the preceding evidence at its original deadline.
            var prices = IronCondorOptionCalculator.Calculate(trade.Legs, risk, DateTime.UtcNow, token);
            var shortPut = risk.Single(scope => !scope.Instruments[0].Instrument.IsCall!.Value
                && trade.Legs.Single(leg => leg.ContractId == scope.Instruments[0].Instrument.ContractId).SignedQuantity < 0);
            var shortCall = risk.Single(scope => scope.Instruments[0].Instrument.IsCall!.Value
                && trade.Legs.Single(leg => leg.ContractId == scope.Instruments[0].Instrument.ContractId).SignedQuantity < 0);
            IronCondorTradePlanInputs inputs;
            lock (entry) inputs = entry.Inputs!;
            inputs = inputs with
            {
                CalculatedSpreadPrices = prices,
                ShortPutGamma = shortPut.Instruments[0].Valuation!.Gamma, ShortCallGamma = shortCall.Instruments[0].Valuation!.Gamma,
                PutOTMProbability = Probability(shortPut), CallOTMProbability = Probability(shortCall),
                ForwardDelta = ForwardDelta(trade.Legs, risk),
                OptionRiskAsOfUtc = risk.Min(scope => scope.EvaluatedAtUtc).UtcDateTime,
                OptionRiskValidUntilUtc = prices.ValidUntilUtc
            };
            if (inputs.StrategyRiskParameterSet is { } policy)
                inputs = inputs with { DailyRiskInputs = new IronCondorTradePlanSnapshotCalculator().CaptureScenarioInputs(
                    trade, position, risk, policy, prices.CalculatedAtUtc, token) };
            token.ThrowIfCancellationRequested();
            lock (entry) entry.Inputs = CopyPricing(entry.Inputs!, inputs);

        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogError(error, "OptionCalculator refresh failed; MethodName={MethodName} TradeId={TradeId} ValueDate={ValueDate}",
                nameof(RefreshPricingAsync), position.Id.Trade.Format(), valueDate);
        }
        finally { lock (entry) entry.PricingRefreshing = false; }
    }

    /// <summary>Copies only current pricing observations, preserving independently refreshed persisted references.</summary>
    /// <param name="references">The current persisted reference observations.</param><param name="pricing">The newly qualified pricing observations.</param>
    /// <returns>An immutable merged input; a delayed reference query cannot overwrite current prices.</returns>
    static IronCondorTradePlanInputs CopyPricing(IronCondorTradePlanInputs references, IronCondorTradePlanInputs pricing)
        => references with
        {
            DailyRiskInputs = pricing.DailyRiskInputs,
            CalculatedSpreadPrices = pricing.CalculatedSpreadPrices, ShortPutGamma = pricing.ShortPutGamma, ShortCallGamma = pricing.ShortCallGamma,
            PutOTMProbability = pricing.PutOTMProbability, CallOTMProbability = pricing.CallOTMProbability, ForwardDelta = pricing.ForwardDelta,
            OptionRiskAsOfUtc = pricing.OptionRiskAsOfUtc, OptionRiskValidUntilUtc = pricing.OptionRiskValidUntilUtc,
            PutForwardPrice = pricing.PutForwardPrice, CallForwardPrice = pricing.CallForwardPrice, LossProbability = pricing.LossProbability,
            DistributionIdentity = pricing.DistributionIdentity, ObservedAtUtc = pricing.ObservedAtUtc
        };

    /// <summary>Captures all four qualified option scopes concurrently; rejects incomplete or skewed risk evidence.</summary>
    /// <param name="legs">The exact established strategy legs.</param><param name="valueDate">The exchange session.</param>
    /// <param name="token">The refresh deadline.</param><returns>Coherent qualified evidence or an empty unavailable result.</returns>
    async Task<MarketCompositionSnapshot[]> ReadOptionRiskAsync(TradeLegDefinition[] legs, DateOnly valueDate, CancellationToken token)
    {
        if (optionRisk is null) return [];
        var snapshots = await Task.WhenAll(legs.Select(leg => optionRisk.CaptureAsync(leg.ContractId, valueDate, token))).ConfigureAwait(false);
        if (snapshots.Any(item => item is null)) return [];
        var values = snapshots.Select(item => item!).ToArray();
        var quotes = values.SelectMany(item => item.Instruments.SelectMany(instrument =>
            new[] { instrument.Instrument.Quote, instrument.Instrument.Underlying })).Where(quote => quote is not null).ToArray();
        return quotes.Length == 8 && values.Select(item => item.GenerationId).Distinct().Count() == 1
            && quotes.Max(quote => quote!.EventAtUtc) - quotes.Min(quote => quote!.EventAtUtc) <= TimeSpan.FromSeconds(1)
            ? values : [];
    }

    /// <summary>Calculates one strategy unit's signed delta from the four matched qualified scopes, independent of quote order.</summary>
    /// <param name="legs">The established trade's exact signed option legs.</param>
    /// <param name="risk">The coherent qualified option valuations for those contracts.</param>
    /// <returns>The signed unit delta, or null when any matched valuation is unavailable.</returns>
    internal static double? ForwardDelta(TradeLegDefinition[] legs, MarketCompositionSnapshot[] risk)
    {
        if (legs.Length != 4 || risk.Length != 4) return null;
        var deltas = new (int SignedQuantity, double OptionDelta)[4];
        for (var index = 0; index < legs.Length; index++)
        {
            var instrument = risk.SelectMany(scope => scope.Instruments)
                .SingleOrDefault(value => value.Instrument.ContractId == legs[index].ContractId);
            if (instrument?.Valuation is not { } value) return null;
            deltas[index] = (legs[index].SignedQuantity, value.Delta);
        }
        return IronCondorLegacyValueInitializers.CalculateForwardDelta(deltas);
    }

    /// <summary>Calculates the recovered legacy OTM probability: z=ln(strike/forward)/(IV*sqrt(years)); put=1-N(z), call=N(z).</summary>
    /// <param name="snapshot">Qualified short-option pricing and underlying quote evidence.</param>
    /// <returns>The probability, or null when any domain denominator is unavailable.</returns>
    internal static double? Probability(MarketCompositionSnapshot? snapshot)
    {
        if (snapshot?.Instruments.SingleOrDefault() is not { } item || item.Valuation is not { } value
            || item.Instrument.Strike is not { } strike || item.Instrument.IsCall is not { } isCall
            || item.Instrument.Underlying is not { } underlying || strike <= 0
            || value.ImpliedVolatility <= 0 || !double.IsFinite(value.ImpliedVolatility)
            || value.TimeToExpiry <= 0 || !double.IsFinite(value.TimeToExpiry)) return null;
        var forward = Convert.ToDouble((underlying.Bid + underlying.Ask) / 2m);
        if (forward <= 0) return null;
        var z = Math.Log(Convert.ToDouble(strike) / forward) / (value.ImpliedVolatility * Math.Sqrt(value.TimeToExpiry));
        var probability = MathNet.Numerics.ExcelFunctions.NormSDist(z);
        return isCall ? probability : 1 - probability;
    }

    /// <summary>Identifies short versus long condors from stable call-leg strikes and actions, without interpreting price sign.</summary>
    /// <param name="legs">The four persisted established option legs.</param>
    /// <returns>The observed short or long Iron Condor strategy.</returns>
    internal static TradeType IdentifyTradeType(TradeLegDefinition[] legs)
    {
        var calls = legs.Where(leg => leg.PutCall == 1).ToArray();
        var puts = legs.Where(leg => leg.PutCall == 2).ToArray();
        if (calls.Length != 2 || puts.Length != 2 || legs.Any(leg => leg.Strike is null || leg.SignedQuantity == 0)
            || legs.Select(leg => Math.Abs(leg.SignedQuantity)).Distinct().Count() != 1
            || calls.Count(leg => leg.SignedQuantity > 0) != 1 || puts.Count(leg => leg.SignedQuantity > 0) != 1)
            throw new ArgumentException("IronCondorMonitoring.LEGS.INVALID: two opposing call and put legs are required.");
        var shortCall = calls.Single(leg => leg.SignedQuantity < 0).Strike!.Value;
        var longCall = calls.Single(leg => leg.SignedQuantity > 0).Strike!.Value;
        var shortPut = puts.Single(leg => leg.SignedQuantity < 0).Strike!.Value;
        var longPut = puts.Single(leg => leg.SignedQuantity > 0).Strike!.Value;
        return (shortCall < longCall, shortPut > longPut) switch
        {
            (true, true) => TradeType.ShortIronCondor,
            (false, false) when shortCall > longCall && shortPut < longPut => TradeType.LongIronCondor,
            _ => throw new ArgumentException("IronCondorMonitoring.LEGS.INVALID: mixed spread direction is not an Iron Condor.")
        };
    }

    sealed class Entry
    {
        public bool Refreshing;
        public bool PricingRefreshing;
        public DateTime NextPricingUtc;
        public EstablishedTradeDefinition? Trade;
        public StrategyPositionSnapshot? Position;
        public string LastPublishedInputs = string.Empty;
        public DateTime NextRefreshUtc;
        public IronCondorTradePlanInputs? Inputs;
    }
}
