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

/// <summary>Loads persisted monitoring references off the tick path and captures current EOD from the authoritative hot cache.</summary>
/// <remarks>One refresh per trade/session runs at a time. Missing providers stay unavailable; old fixture rows and fabricated
/// distributions are never substituted. This is a read cache, not command state or a trade-plan replay queue.</remarks>
public sealed class IronCondorMonitoringInputReader(IDbContextFactory databases, ILogger logger, IFinancialQueryStore? financialQueries = null, IndividualOptionRiskReader? optionRisk = null,
    Func<InitializeIronCondorMonitoringCommand, CancellationToken, Task>? initializeMonitoring = null,
    Func<InsertSpreadDistributionCommand, CancellationToken, Task>? recordDistributions = null,
    Func<TradeLegDefinition[], DateOnly, CancellationToken, Task<MarketCompositionSnapshot[]>>? captureOptionRisk = null,
    CancellationToken stoppingToken = default,
    Func<StrategyPositionSnapshot, DateOnly, IronCondorTradePlanInputs, DateTime, Task>? publishInputChange = null)
{
    int observerStarted;
    readonly ConcurrentDictionary<(TradeEntityId, DateOnly), Entry> entries = new();
    readonly ConcurrentDictionary<DateOnly, Lazy<Task<double[]>>> baselines = new();
    readonly ConcurrentDictionary<(string ContractId, DateOnly ValueDate), Lazy<Task<double?>>> dailyMovingAverages = new();

    /// <summary>Returns captured references immediately and schedules bounded background refreshes when due.</summary>
    /// <param name="position">The coherent observed position and full trade identity.</param>
    /// <param name="valueDate">The active exchange session.</param>
    /// <param name="nowUtc">The captured evaluation instant.</param>
    /// <returns>Available observations, or null while the initial provider read is pending.</returns>
    public IronCondorTradePlanInputs? Capture(StrategyPositionSnapshot position, DateOnly valueDate, DateTime nowUtc)
    {
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
            CurrentFuturesEodCache.Shared.TryGet(entry.UnderlyingContractId, valueDate, out var live);
            return inputs.OptionRiskValidUntilUtc is { } until && until <= nowUtc
                ? inputs with { UnderlyingStatistics = live ?? inputs.UnderlyingStatistics, ShortPutGamma = null, ShortCallGamma = null,
                    PutOTMProbability = null, CallOTMProbability = null, ForwardDelta = null, CalculatedSpreadPrices = null }
                : inputs with { UnderlyingStatistics = live ?? inputs.UnderlyingStatistics };
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
                            ShortCallGamma = null, ForwardDelta = null, PutOTMProbability = null, CallOTMProbability = null };
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
                foreach (var baseline in baselines.Where(item => item.Key < date.AddDays(-1)).ToArray()) baselines.TryRemove(baseline);
                foreach (var average in dailyMovingAverages.Where(item => item.Key.ValueDate < date.AddDays(-1)).ToArray()) dailyMovingAverages.TryRemove(average);
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
            var contract = await databases.SecuritiesDb.GetFuturesOptionContractAsync(trade.Legs[0].ContractId, token).WaitAsync(token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("IronCondorMonitoring.REFERENCE.UNAVAILABLE: exact option definition is required.");
            var expiries = trade.Legs.Select(leg => leg.Expiry).Distinct().ToArray();
            if (expiries.Length != 1 || expiries[0] is not { } expiry)
                throw new InvalidOperationException("IronCondorMonitoring.EXPIRY.INVALID: matching four-leg expiry required.");
            var tradeType = IdentifyTradeType(trade.Legs);
            var cashTask = ReadFundCashAsync(trade.Id, token);
            var limitsTask = databases.TradeDb.GetTradeLimitAsync(trade.Id.TradeId, token);
            var signalTask = databases.MarketDataDb.GetLastFuturesTradeSignalAsync(contract.UnderlyingContractId, valueDate, token);
            var classificationTask = databases.TradeDb.GetTradePlanForwardLossLimitAsync(trade.Id.OrderId, trade.Id.TradeId, valueDate, tradeType);
            var putType = tradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
            var callType = tradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
            var putTask = databases.OptionPricerDb.GetSpreadDistributionAsync(trade.Id.TradeId, putType, TradeStatus.IntraDay, valueDate, expiry.DayNumber - valueDate.DayNumber, token);
            var callTask = databases.OptionPricerDb.GetSpreadDistributionAsync(trade.Id.TradeId, callType, TradeStatus.IntraDay, valueDate, expiry.DayNumber - valueDate.DayNumber, token);
            var dailyKey = (contract.UnderlyingContractId, valueDate);
            var dailyLoad = dailyMovingAverages.GetOrAdd(dailyKey, key => new(() => LoadFiveDayXmaAsync(key.ContractId, key.ValueDate)));
            var dailyTask = dailyLoad.Value;
            var baselineTask = baselines.GetOrAdd(valueDate, date => new(() => LoadBaselineAsync(date))).Value;
            await Task.WhenAll(limitsTask, signalTask, classificationTask, putTask, callTask, baselineTask, cashTask, dailyTask).WaitAsync(token).ConfigureAwait(false);
            var fiveDayXma = await dailyTask;
            if (fiveDayXma is null)
                dailyMovingAverages.TryRemove(new KeyValuePair<(string, DateOnly), Lazy<Task<double?>>>(dailyKey, dailyLoad));
            var limits = await limitsTask;
            if (limits?.TradeType != tradeType) limits = null;
            var put = await putTask;
            var call = await callTask;
            var distributionsMatch = put is not null && call is not null && put.ValueDate == valueDate && call.ValueDate == valueDate
                && put.TradeId == trade.Id.TradeId && call.TradeId == trade.Id.TradeId
                && put.TradeType == putType && call.TradeType == callType
                && double.IsFinite(put.ForwardPrice) && double.IsFinite(call.ForwardPrice);
            CurrentFuturesEodCache.Shared.TryGet(contract.UnderlyingContractId, valueDate, out var eod);
            var signal = await signalTask;
            var financial = await cashTask;
            var inputs = new IronCondorTradePlanInputs
            {
                FundBalance = financial?.Value?.AvailableCash, FundFinancialRevision = financial?.FinancialRevision,
                FundCashAsOfUtc = financial?.ObservedAtUtc,
                ContractCashMultiplier = trade.Legs.Select(leg => leg.CashMultiplier).Distinct().Count() == 1
                    && trade.Legs[0].CashMultiplier > 0 ? trade.Legs[0].CashMultiplier : null,
                OpeningCommission = trade.OpeningCommission,
                FiveDayXMA = fiveDayXma,
                TradeType = tradeType, TradeDate = FuturesTradingValueDate.TryGet(new DateTimeOffset(trade.EstablishedAtUtc), out var openingDate) ? openingDate : null,
                MaturityDate = expiry, TradeLimits = limits, TradeSignal = signal, UnderlyingStatistics = eod,
                FiftyDayMA = signal?.FiftyDMA is > 0 ? Convert.ToDouble(signal.FiftyDMA) : null,
                HistoricalForwardLossRatios = await baselineTask,
                BaselineIdentity = $"Scylla/trade_plan_forward_loss_ratio/{valueDate.AddDays(-60):yyyyMMdd}/{valueDate.AddDays(-1):yyyyMMdd}",
                DistributionIdentity = distributionsMatch ? $"Scylla/spread_distribution/{put!.Id}/{call!.Id}" : string.Empty,
                ObservedAtUtc = distributionsMatch ? (put!.CreatedOn < call!.CreatedOn ? put.CreatedOn : call.CreatedOn) : default,
                PutForwardPrice = distributionsMatch ? Convert.ToDecimal(put!.ForwardPrice) : 0,
                CallForwardPrice = distributionsMatch ? Convert.ToDecimal(call!.ForwardPrice) : 0,
                ForwardLossPriceLimit = tradeType == TradeType.ShortIronCondor ? limits?.MaxLossLimit ?? 0 : limits?.MinProfitLimit ?? 0,
                LossProbability = distributionsMatch ? Math.Max(put!.LossProbability, call!.LossProbability) : null,
                ForwardLossLimit = (await classificationTask)?.LimitType
            };
            token.ThrowIfCancellationRequested();
            lock (entry)
            {
                // A reference load cannot roll back fresher calculator observations.
                if (entry.Inputs is { } current && current.CalculatedSpreadPrices is not null)
                    inputs = CopyPricing(inputs, current);
                entry.Inputs = inputs; entry.Trade = trade; entry.UnderlyingContractId = contract.UnderlyingContractId;
            }
            if ((limits is null || limits.MaxLoss >= 0 || limits.MaxProfit <= 0) && initializeMonitoring is not null && financialQueries is not null)
                await RequestInitializationAsync(position, trade, financial, valueDate, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (baselines.TryGetValue(valueDate, out var baseline) && baseline.IsValueCreated && baseline.Value.IsFaulted)
                baselines.TryRemove(new KeyValuePair<DateOnly, Lazy<Task<double[]>>>(valueDate, baseline));
            foreach (var daily in dailyMovingAverages.Where(item => item.Key.ValueDate == valueDate
                && item.Value.IsValueCreated && (item.Value.Value.IsFaulted || item.Value.Value.IsCanceled)))
                dailyMovingAverages.TryRemove(daily);
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
            InsertSpreadDistributionCommand? insert = null;
            // A forward-model failure must not discard a valid current OptionCalculator spread price.
            try
            {
                if (inputs.TradeLimits?.MaxLoss is < 0)
                {
                    var paired = IronCondorMonitoringDistributionCompute.Calculate(trade, position, prices, risk, inputs.TradeLimits, valueDate);
                    inputs = inputs with
                    {
                        PutForwardPrice = (decimal)paired.Put.ForwardPrice, CallForwardPrice = (decimal)paired.Call.ForwardPrice,
                        LossProbability = Math.Max(paired.Put.LossProbability, paired.Call.LossProbability),
                        DistributionIdentity = $"OptionCalculatorForward/v1/{paired.Put.Id}/{paired.Call.Id}", ObservedAtUtc = prices.CalculatedAtUtc
                    };
                    insert = new(paired.Put, paired.Call)
                    {
                        CommandId = TradePlanContractIdentity.DeterministicId($"{trade.Id.Format()}|OptionCalculatorForward/v1|{prices.CalculatedAtUtc.Ticks}"),
                        PostEvents = true, Subject = new(ActorType.Command, InsertSpreadDistributionCommand.Actor,
                            InsertSpreadDistributionCommand.Verb, paired.Put.EntityId.Format())
                    };
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                logger.LogError(error, "Forward spread calculation unavailable; MethodName={MethodName} TradeId={TradeId} ValueDate={ValueDate}",
                    nameof(RefreshPricingAsync), trade.Id.Format(), valueDate);
            }
            token.ThrowIfCancellationRequested();
            lock (entry)
            {
                entry.Inputs = CopyPricing(entry.Inputs!, inputs);
                // One bounded history request at a time; excess observations are disposable, never queued.
                if (insert is not null && recordDistributions is not null && !entry.HistoryWriting && prices.CalculatedAtUtc >= entry.NextHistoryUtc)
                {
                    entry.HistoryWriting = true; entry.NextHistoryUtc = prices.CalculatedAtUtc.AddSeconds(5);
                    _ = Task.Run(() => RecordDistributionAsync(entry, insert, trade.Id, valueDate));
                }
            }
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
            CalculatedSpreadPrices = pricing.CalculatedSpreadPrices, ShortPutGamma = pricing.ShortPutGamma, ShortCallGamma = pricing.ShortCallGamma,
            PutOTMProbability = pricing.PutOTMProbability, CallOTMProbability = pricing.CallOTMProbability, ForwardDelta = pricing.ForwardDelta,
            OptionRiskAsOfUtc = pricing.OptionRiskAsOfUtc, OptionRiskValidUntilUtc = pricing.OptionRiskValidUntilUtc,
            PutForwardPrice = pricing.PutForwardPrice, CallForwardPrice = pricing.CallForwardPrice, LossProbability = pricing.LossProbability,
            DistributionIdentity = pricing.DistributionIdentity, ObservedAtUtc = pricing.ObservedAtUtc
        };

    /// <summary>Attempts a distribution source command once after current pricing has already become available.</summary>
    /// <param name="entry">The single in-flight history slot.</param><param name="command">The immutable paired observations.</param>
    /// <param name="tradeId">The full logging identity.</param><param name="valueDate">The observation session.</param>
    /// <returns>The bounded write attempt; failures are logged without retrying or withdrawing current values.</returns>
    async Task RecordDistributionAsync(Entry entry, InsertSpreadDistributionCommand command, TradeEntityId tradeId, DateOnly valueDate)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await recordDistributions!(command, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogError(error, "Spread distribution request dropped; MethodName={MethodName} CommandId={CommandId} TradeId={TradeId} ValueDate={ValueDate}",
                nameof(RecordDistributionAsync), command.CommandId, tradeId.Format(), valueDate);
        }
        finally { lock (entry) entry.HistoryWriting = false; }
    }

    /// <summary>Requests a source-backed limit initialization using matching accepted order and ledger observations.</summary>
    /// <param name="position">The current exact position.</param><param name="trade">Its persisted established trade.</param>
    /// <param name="cash">Authoritative available Fund cash and revision.</param><param name="valueDate">The active session.</param>
    /// <param name="token">The background refresh deadline.</param>
    /// <returns>The bounded initialization request; no read model is written by this reader.</returns>
    async Task RequestInitializationAsync(StrategyPositionSnapshot position, EstablishedTradeDefinition trade,
        FinancialRead<FinancialBalanceSnapshot>? cash, DateOnly valueDate, CancellationToken token)
    {
        if (cash?.Value?.AvailableCash is not > 0) return;
        var order = await databases.TradeDb.GetTradeOrderAsync(new(trade.Id.PortfolioId, trade.Id.FundId, trade.Id.OrderId), token)
            .WaitAsync(token).ConfigureAwait(false);
        // A multi-component order needs an explicit component capital allocation; never assign its total to one trade.
        if (order is null || order.Components.Length != 1 || order.Components[0].ComponentId != trade.SourceComponentId
            || order.RequiredCapital < 0 || order.Revision <= 0) return;
        var command = new InitializeIronCondorMonitoringCommand
        {
            // Each capture has different cash/time evidence; its request identity must be unique.
            CommandId = Guid.NewGuid(),
            Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Command, PositionActorNames.IronCondorCommand,
                InitializeIronCondorMonitoringCommand.Verb, position.Id.Format()),
            EntityId = position.Id,
            MonitoringInitialization = new()
            {
                IronCondorTrade = trade, FundAvailableCash = cash.Value.AvailableCash, FundFinancialRevision = cash.FinancialRevision,
                FundCashAsOfUtc = cash.ObservedAtUtc, RequiredCapital = order.RequiredCapital, TradeOrderRevision = order.Revision,
                // Capture after the ledger/order reads so their receipt time cannot be in the future.
                ValueDate = valueDate, InitializedAtUtc = DateTime.UtcNow
            }
        };
        await initializeMonitoring!(command, token).ConfigureAwait(false);
    }

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

    /// <summary>Loads completed daily closes once per contract/session, orders them by exchange date and calculates
    /// the five-session EMA. The current incomplete session is excluded; unavailable history stays null.</summary>
    /// <param name="underlyingContractId">The option's exact underlying futures contract.</param>
    /// <param name="valueDate">The open exchange session, excluded from the daily close sample.</param>
    /// <returns>The EMA seeded by five completed sessions, or null if that seed is unavailable.</returns>
    async Task<double?> LoadFiveDayXmaAsync(string underlyingContractId, DateOnly valueDate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var contract = await databases.SecuritiesDb.GetFuturesContractAsync(underlyingContractId, timeout.Token)
            .WaitAsync(timeout.Token).ConfigureAwait(false);
        if (contract is null || string.IsNullOrWhiteSpace(contract.Symbol)) return null;
        var closes = await databases.MarketDataDb.GetFuturesEodClosingPricesAsync(underlyingContractId, contract.Symbol,
            valueDate.AddDays(-120), valueDate.AddDays(-1), 60).WaitAsync(timeout.Token).ConfigureAwait(false);
        var completed = closes.Where(close => close.ValueDate < valueDate && close.ValueDate >= valueDate.AddDays(-120)
                && close.Symbol == contract.Symbol).OrderBy(close => close.ValueDate).ToArray();
        if (completed.Length < 5 || completed.Select(close => close.ValueDate).Distinct().Count() != completed.Length)
            return null;
        return IronCondorLegacyValueInitializers.CalculateFiveDayXma(completed.Select(close => close.ClosingPrice).ToArray());
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

    /// <summary>Captures available Fund cash from the authoritative ledger under a read-only server scope.</summary>
    /// <param name="tradeId">The established trade's exact Portfolio and Fund identity.</param>
    /// <param name="token">The bounded input refresh deadline.</param>
    /// <returns>Observed ledger cash; missing or unavailable financial reads remain null.</returns>
    async Task<FinancialRead<FinancialBalanceSnapshot>?> ReadFundCashAsync(TradeEntityId tradeId, CancellationToken token)
    {
        if (financialQueries is null) return null;
        var result = await financialQueries.ReadAsync(new FinancialReadScope
        {
            PortfolioId = tradeId.PortfolioId, FundId = tradeId.FundId,
            Access = new("IronCondorTradePlanMonitoring", ["LedgerRead"], [tradeId.PortfolioId])
        }, new GetAccountBalancesRequest(), token).ConfigureAwait(false);
        return result.Status == FinancialReadStatus.Found ? result : null;
    }

    /// <summary>Loads the preceding sixty calendar days once per session, excluding the current session.</summary>
    /// <param name="valueDate">The current session, excluded from the historical baseline.</param>
    /// <returns>Persisted finite nonnegative forward-loss observations; an empty baseline remains unavailable.</returns>
    async Task<double[]> LoadBaselineAsync(DateOnly valueDate)
    {
        var values = await databases.TradeDb.GetTradePlanForwardLossRatiosAsync(valueDate.AddDays(-60), valueDate.AddDays(-1))
            .WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        return values.Select(value => value.ForwardLossRatio).Where(value => double.IsFinite(value) && value >= 0).ToArray();
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
        public bool HistoryWriting;
        public DateTime NextPricingUtc;
        public DateTime NextHistoryUtc;
        public EstablishedTradeDefinition? Trade;
        public StrategyPositionSnapshot? Position;
        public string LastPublishedInputs = string.Empty;
        public DateTime NextRefreshUtc;
        public IronCondorTradePlanInputs? Inputs;
        public string UnderlyingContractId = string.Empty;
    }
}
