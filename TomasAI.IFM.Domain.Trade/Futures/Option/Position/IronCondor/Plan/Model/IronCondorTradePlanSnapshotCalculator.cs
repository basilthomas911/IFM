using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Creates daily-loss snapshots from frozen position, broker-currency and OptionCalculator scenario observations.</summary>
/// <remarks>All money values use the broker contract currency. No database access, retry, historical replay or inferred scenario occurs here.</remarks>
public sealed class IronCondorTradePlanSnapshotCalculator
{
    /// <summary>Prices all legs under both adverse futures moves and selects the scenario with the lowest projected signed position value.</summary>
    /// <remarks>Scenario time advances by the configured horizon. IV shifts by configured percentage points; currency is read from all four broker contracts.
    /// Current prices and Greeks are evaluated by OptionCalculator. Forward pricing runs outside tick admission.</remarks>
    public IronCondorDailyRiskInputs CaptureScenarioInputs(EstablishedTradeDefinition trade, StrategyPositionSnapshot position,
        MarketCompositionSnapshot[] risk, StrategyRiskParameterSet parameterSet, DateTime atUtc, CancellationToken token = default)
    {
        parameterSet.Validate();
        var policy = parameterSet.IronCondor!;
        var current = IronCondorOptionCalculator.Calculate(trade.Legs, risk, atUtc, token);
        var contracts = risk.Select(scope => scope.Instruments.Single().Instrument.Pricing!.Contract).ToArray();
        if (contracts.Any(contract => contract.Root != parameterSet.InstrumentRoot) || trade.Id != position.Id.Trade
            || contracts.Select(c => c.Currency).Distinct(StringComparer.Ordinal).Count() != 1
            || string.IsNullOrWhiteSpace(contracts[0].Currency))
            throw new ArgumentException("IronCondorRisk.CONTRACT_POLICY_OR_CURRENCY_MISMATCH");
        // Qualified scopes may capture consecutive futures ticks. Reprice all four legs against
        // the newest shared-contract quote, rather than requiring identical prices across scopes.
        var underlying = risk.Select(scope => scope.Instruments.Single().Instrument.Underlying!)
            .OrderByDescending(quote => quote.EventAtUtc).First();
        var future = (underlying.Bid + underlying.Ask) / 2m;
        var multiplier = contracts[0].Multiplier;
        var markLegs = position.Legs.ToDictionary(l => l.TradeLegId);
        var calculator = new OptionCalculator();
        var observationSets = new List<(decimal Future, IronCondorTradePlanLegObservation[] Legs)>();
        foreach (var direction in new[] { -1, 1 })
        {
            token.ThrowIfCancellationRequested();
            var scenarioFuture = future + direction * policy.AdverseFuturesMovePoints;
            if (scenarioFuture <= 0) throw new ArgumentException("IronCondorRisk.SCENARIO_UNDERLYING_NONPOSITIVE");
            var observations = new List<IronCondorTradePlanLegObservation>();
            foreach (var leg in trade.Legs)
            {
                token.ThrowIfCancellationRequested();
                var scope = risk.Single(scope => scope.Instruments.Single().Instrument.ContractId == leg.ContractId);
                var item = scope.Instruments.Single();
                var contract = item.Instrument.Pricing!.Contract;
                var rate = item.Instrument.Pricing.Rate.AnnualContinuousRate;
                var iv = item.Valuation!.ImpliedVolatility;
                var isCall = leg.PutCall == 1;
                var nowYears = OptionPricingQualification.YearFraction(contract, new DateTimeOffset(atUtc));
                var forwardYears = OptionPricingQualification.YearFraction(contract,
                    new DateTimeOffset(atUtc.AddSeconds(policy.ScenarioHorizonSeconds)));
                if (forwardYears <= 0) throw new ArgumentException("IronCondorRisk.SCENARIO_REACHES_EXPIRY");
                var nowRequest = Black76PricingModel.CreateRequest(contract, (double)future, leg.Strike!.Value, isCall, nowYears, rate);
                var forwardRequest = Black76PricingModel.CreateRequest(contract, (double)scenarioFuture, leg.Strike.Value, isCall, forwardYears, rate);
                var spotResult = calculator.Price(nowRequest, iv, token);
                var forwardResult = calculator.PriceAndDelta(forwardRequest,
                    iv + policy.ScenarioVolatilityShiftPercentagePoints / 100, token);
                if (!spotResult.Success || !forwardResult.Success)
                    throw new InvalidOperationException($"IronCondorRisk.CALCULATOR_FAILED;Contract={leg.ContractId};Current={spotResult.Failure};Forward={forwardResult.Failure}");
                var spot = spotResult.Value!.Value;
                var forward = forwardResult.Value!.Value;
                var mark = markLegs[leg.TradeLegId];
                observations.Add(new()
                {
                    TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                    LegRole = $"{(leg.SignedQuantity < 0 ? "Short" : "Long")}{(isCall ? "Call" : "Put")}",
                    Strike = leg.Strike, Expiry = leg.Expiry, PutCall = leg.PutCall, SignedQuantity = mark.SignedQuantity,
                    BidPrice = item.Instrument.Quote!.Bid, AskPrice = item.Instrument.Quote.Ask, MarkPrice = mark.CurrentPrice,
                    TheoreticalPrice = (decimal)spot.Price, ForwardPrice = (decimal)forward.Price,
                    ImpliedVolatility = iv, Delta = spot.Delta, Gamma = spot.Gamma,
                    Vega = spot.Vega / 100, Theta = spot.Theta / (contract.DayCount == PricingDayCount.Actual360 ? 360 : 365),
                    ForwardDelta = forward.Delta, QuoteAtUtc = item.Instrument.Quote.EventAtUtc.UtcDateTime,
                    RiskCalculatedAtUtc = atUtc
                });
            }
            observationSets.Add((scenarioFuture, observations.ToArray()));
        }
        var selected = observationSets.OrderBy(s => s.Legs.Sum(l => l.ForwardPrice!.Value * l.SignedQuantity!.Value)).First();
        var commission = selected.Legs.Sum(l => Math.Abs(l.SignedQuantity!.Value)) * policy.ExitCommissionPerContract;
        // Bid/ask crossing relative to model price plus an explicit additional tick allowance. Never add crossing twice.
        var currentValue = selected.Legs.Sum(l => l.TheoreticalPrice!.Value * l.SignedQuantity!.Value) * multiplier;
        var closeValue = CalculateCloseValue(selected.Legs, multiplier)!.Value;
        var crossing = Math.Max(0m, currentValue - closeValue);
        var tickAllowance = selected.Legs.Sum(l =>
        {
            var convention = contracts.Single(c => c.ContractId == l.ContractId);
            var closingPrice = l.SignedQuantity > 0 ? l.BidPrice!.Value : l.AskPrice!.Value;
            return OptionPremiumTicks.GetIncrement(convention, closingPrice, combinationLeg: true)
                * Math.Abs(l.SignedQuantity!.Value) * policy.AdditionalExitSlippageTicksPerLeg * multiplier;
        });
        var openingDate = FuturesTradingValueDate.TryGet(new DateTimeOffset(trade.EstablishedAtUtc), out var opened) ? opened : default;
        var actualDailyCommission = (openingDate == position.ValueDate ? trade.OpeningCommission : 0)
            + position.ClosingFills.Where(fill => FuturesTradingValueDate.TryGet(new DateTimeOffset(fill.FilledAtUtc), out var date)
                && date == position.ValueDate).Sum(fill => fill.Commission);
        return new()
        {
            DailyPnl = position.DailyPnl * multiplier - actualDailyCommission,
            DailyLossLimit = policy.DailyLossLimit, ContractMultiplier = multiplier, Currency = contracts[0].Currency,
            UnderlyingPrice = future, ScenarioHorizonSeconds = policy.ScenarioHorizonSeconds,
            ScenarioUnderlyingPrice = selected.Future, ScenarioVolatilityShift = policy.ScenarioVolatilityShiftPercentagePoints,
            ScenarioKind = selected.Future < future ? "AdverseFuturesDown" : "AdverseFuturesUp",
            PricingModelVersion = current.CalculatorVersion + "/" + current.NumericalPolicyVersion,
            EstimatedExitCommission = commission, EstimatedExitSlippage = crossing + tickAllowance,
            RiskValidUntilUtc = current.ValidUntilUtc,
            TimeToExpiryDays = (contracts.Max(contract => contract.ExpirationUtc).UtcDateTime - atUtc).TotalDays,
            StrategyRiskParameterSet = parameterSet, Legs = selected.Legs,
            PositionSequence = position.PositionSequence, RouteGeneration = position.RouteGeneration,
            ActualDailyCommission = actualDailyCommission, ValueDate = position.ValueDate,
            PutOTMProbability = IronCondorMonitoringInputReader.Probability(risk.Single(scope => scope.Instruments.Single().Instrument.ContractId == trade.Legs.Single(l => l.PutCall == 2 && l.SignedQuantity < 0).ContractId)),
            CallOTMProbability = IronCondorMonitoringInputReader.Probability(risk.Single(scope => scope.Instruments.Single().Instrument.ContractId == trade.Legs.Single(l => l.PutCall == 1 && l.SignedQuantity < 0).ContractId))
        };
    }

    /// <summary>Preserves known daily PnL when scenario quotes are unavailable; forward-loss values remain unavailable.</summary>
    /// <remarks>Daily currency PnL is the current position's point PnL times its observed contract multiplier,
    /// less actual opening and closing commissions attributable to this value date. No currency or quote is invented.</remarks>
    public static IronCondorDailyRiskInputs CaptureUnavailableInputs(StrategyPositionSnapshot position, IronCondorTradePlanInputs? inputs)
        => new()
        {
            ContractMultiplier = inputs?.ContractCashMultiplier, ValueDate = position.ValueDate,
            RouteGeneration = position.RouteGeneration, PositionSequence = position.PositionSequence,
            StrategyRiskParameterSet = inputs?.StrategyRiskParameterSet,
            DailyLossLimit = inputs?.StrategyRiskParameterSet?.IronCondor?.DailyLossLimit,
            ActualDailyCommission = (inputs?.TradeDate == position.ValueDate ? inputs.OpeningCommission ?? 0 : 0)
                + position.ClosingFills.Where(fill => FuturesTradingValueDate.TryGet(new DateTimeOffset(fill.FilledAtUtc), out var date)
                    && date == position.ValueDate).Sum(fill => fill.Commission)
        };

    /// <summary>Populates identity, prices, exposures and daily-loss metrics. Missing observations remain null.</summary>
    public IronCondorTradePlanSnapshot Create(StrategyTradePlanSnapshot plan, Guid sourceEventId,
        DateOnly? tradeDate, DateOnly? maturityDate, IronCondorDailyRiskInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(inputs);
        inputs.StrategyRiskParameterSet?.Validate();
        var policy = inputs.StrategyRiskParameterSet?.IronCondor;
        var position = plan.Position;
        var observations = inputs.Legs.Select(leg => leg with { MarkPrice = position.Legs.FirstOrDefault(mark => mark.TradeLegId == leg.TradeLegId)?.CurrentPrice }).ToArray();
        // Model scenario quotes can serve successive marks until their deadline. Daily PnL always comes from this position.
        if (inputs.ContractMultiplier is > 0)
            inputs = inputs with { DailyPnl = position.DailyPnl * inputs.ContractMultiplier.Value - inputs.ActualDailyCommission };
        var reasons = new List<string>();
        if (inputs.ValueDate != plan.ValueDate || inputs.RouteGeneration != position.RouteGeneration
            || observations.Any(l => !position.Legs.Any(p => p.TradeLegId == l.TradeLegId && p.ContractId == l.ContractId && p.SignedQuantity == l.SignedQuantity)))
            reasons.Add("Scenario pricing belongs to another value date, position quantity, contract or generation.");
        if (policy is null || inputs.DailyLossLimit != policy.DailyLossLimit
            || inputs.ScenarioHorizonSeconds != policy.ScenarioHorizonSeconds
            || inputs.ScenarioVolatilityShift != policy.ScenarioVolatilityShiftPercentagePoints)
            reasons.Add("The exact strategy risk parameter set must match the captured limit, horizon and IV scenario.");
        var quantity = observations.Length == 4 && observations.All(l => l.SignedQuantity is not null and not 0)
            && observations.Select(l => Math.Abs(l.SignedQuantity!.Value)).Distinct().Count() == 1
            ? Math.Abs(observations[0].SignedQuantity!.Value) : (int?)null;
        if (quantity is null || observations.Select(l => l.TradeLegId).Distinct().Count() != 4
            || observations.Any(l => l.TradeLegId == Guid.Empty)
            || !observations.Select(l => l.TradeLegId).Order().SequenceEqual(position.Legs.Select(l => l.TradeLegId).Order())
            || observations.Count(l => l.PutCall == 1) != 2 || observations.Count(l => l.PutCall == 2) != 2
            || observations.GroupBy(l => l.PutCall).Any(g => g.Sum(l => l.SignedQuantity ?? 0) != 0))
            reasons.Add("Four matching, balanced option legs with equal nonzero strategy quantities are required.");
        if (inputs.DailyPnl is null || inputs.DailyLossLimit is null or <= 0 || inputs.ContractMultiplier is null or <= 0
            || string.IsNullOrWhiteSpace(inputs.Currency))
            reasons.Add("Explicit daily currency PnL, positive daily loss limit, broker multiplier and broker currency are required.");
        if (inputs.ScenarioHorizonSeconds is null or <= 0 || inputs.ScenarioUnderlyingPrice is null or <= 0
            || inputs.UnderlyingPrice is null or <= 0 || !Finite(inputs.ScenarioVolatilityShift)
            || string.IsNullOrWhiteSpace(inputs.ScenarioKind) || string.IsNullOrWhiteSpace(inputs.PricingModelVersion))
            reasons.Add("An explicit horizon, coherent futures/IV scenario and pricing version are required.");
        if (inputs.EstimatedExitCommission is null or < 0 || inputs.EstimatedExitSlippage is null or < 0)
            reasons.Add("Additional closing commission and nonduplicated slippage estimates are required.");
        var oldest = observations.Length == 4 && observations.All(l => l.QuoteAtUtc.HasValue)
            ? observations.Min(l => l.QuoteAtUtc) : null;
        var stale = oldest is null || oldest.Value.Kind != DateTimeKind.Utc || oldest > plan.CalculatedAtUtc
            || plan.CalculatedAtUtc - oldest > TimeSpan.FromSeconds(policy?.MaximumQuoteAgeSeconds ?? plan.Parameters.MaximumDataAgeSeconds)
            || inputs.RiskValidUntilUtc is null || inputs.RiskValidUntilUtc.Value.Kind != DateTimeKind.Utc
            || inputs.RiskValidUntilUtc <= plan.CalculatedAtUtc;
        if (stale) reasons.Add("Four fresh quotes and unexpired risk inputs are required.");
        if (observations.Any(l => l.MarkPrice is null or <= 0 || l.TheoreticalPrice is null or < 0 || l.ForwardPrice is null or < 0))
            reasons.Add("Current and scenario option-leg prices are required.");
        var snapshot = new IronCondorTradePlanSnapshot
        {
            OrderId = position.Id.Trade.OrderId, TradeId = position.Id.Trade.TradeId,
            ValueDate = plan.ValueDate, TradeDate = tradeDate, MaturityDate = maturityDate,
            ActionDateTime = plan.CalculatedAtUtc, ActionDate = plan.CalculatedAtUtc, SequenceId = plan.PlanRevision,
            Position = position, SourceEventId = sourceEventId, Legs = observations,
            PutOTMProbability = inputs.PutOTMProbability, CallOTMProbability = inputs.CallOTMProbability,
            Quantity = quantity, ContractMultiplier = inputs.ContractMultiplier, Currency = inputs.Currency,
            DailyPnl = inputs.DailyPnl, DailyLoss = inputs.DailyPnl is { } pnl ? CalculateDailyLoss(pnl) : null,
            DailyLossLimit = inputs.DailyLossLimit, EstimatedExitCommission = inputs.EstimatedExitCommission,
            EstimatedExitSlippage = inputs.EstimatedExitSlippage, UnderlyingPrice = inputs.UnderlyingPrice,
            ScenarioHorizonSeconds = inputs.ScenarioHorizonSeconds, ScenarioUnderlyingPrice = inputs.ScenarioUnderlyingPrice,
            ScenarioUnderlyingMove = inputs.ScenarioUnderlyingPrice - inputs.UnderlyingPrice,
            ScenarioVolatilityShift = inputs.ScenarioVolatilityShift, ScenarioKind = inputs.ScenarioKind,
            PricingModelVersion = inputs.PricingModelVersion, OldestQuoteAtUtc = oldest,
            RiskValidUntilUtc = inputs.RiskValidUntilUtc, TimeToExpiryDays = inputs.TimeToExpiryDays,
            StrategyRiskParameterSet = inputs.StrategyRiskParameterSet, RiskParameterSetHash = inputs.StrategyRiskParameterSet?.Hash(),
            CalculationVersion = "IronCondor/DailyForwardLoss/v1"
        };
        if (quantity is { } q && reasons.All(r => !r.StartsWith("Four matching", StringComparison.Ordinal)))
        {
            snapshot = snapshot with
            {
                PutSpreadPrice = CalculateSpreadPrice(observations, 2, q, false),
                CallSpreadPrice = CalculateSpreadPrice(observations, 1, q, false),
                PutForwardPrice = CalculateSpreadPrice(observations, 2, q, true),
                CallForwardPrice = CalculateSpreadPrice(observations, 1, q, true),
                NetDelta = CalculateExposure(observations, q, l => l.Delta),
                NetGamma = CalculateExposure(observations, q, l => l.Gamma),
                NetVega = CalculateExposure(observations, q, l => l.Vega),
                NetTheta = CalculateExposure(observations, q, l => l.Theta),
                ForwardDelta = CalculateExposure(observations, q, l => l.ForwardDelta),
                DistanceToShortPut = inputs.UnderlyingPrice - observations.Single(l => l.PutCall == 2 && l.SignedQuantity < 0).Strike,
                DistanceToShortCall = observations.Single(l => l.PutCall == 1 && l.SignedQuantity < 0).Strike - inputs.UnderlyingPrice
            };
            snapshot = snapshot with { CombinedSpreadPrice = snapshot.PutSpreadPrice + snapshot.CallSpreadPrice,
                CombinedForwardPrice = snapshot.PutForwardPrice + snapshot.CallForwardPrice };
            snapshot = snapshot with {
                CurrentPositionValue = snapshot.CombinedSpreadPrice * q * inputs.ContractMultiplier,
                ForwardPositionValue = snapshot.CombinedForwardPrice * q * inputs.ContractMultiplier,
                EstimatedCloseValue = CalculateCloseValue(observations, inputs.ContractMultiplier) };
        }
        snapshot = snapshot with { LossHeadroom = snapshot.DailyLossLimit - snapshot.DailyLoss };
        if (reasons.Count == 0)
        {
            var costs = inputs.EstimatedExitCommission!.Value + inputs.EstimatedExitSlippage!.Value;
            var change = snapshot.ForwardPositionValue!.Value - snapshot.CurrentPositionValue!.Value;
            var forwardPnl = CalculateForwardDailyPnl(inputs.DailyPnl!.Value, change, costs);
            var loss = CalculateDailyLoss(forwardPnl);
            var ratio = CalculateForwardLossRatio(loss, inputs.DailyLossLimit!.Value);
            snapshot = snapshot with { EstimatedExitCosts = costs, ProjectedPositionValueChange = change,
                ForwardDailyPnl = forwardPnl, ForwardLoss = loss, ForwardLossRatio = ratio,
                ForwardLossHeadroom = inputs.DailyLossLimit - loss,
                ExitRecommended = inputs.DailyPnl < 0 && ratio >= policy!.ExitForwardLossRatio,
                ExitReason = inputs.DailyPnl < 0 && ratio >= policy!.ExitForwardLossRatio ? "Daily forward loss reaches configured limit." : "Daily forward loss trigger is not reached." };
        }
        var warning = false;
        var exit = snapshot.ExitRecommended == true;
        warning = !exit && snapshot.DailyPnl < 0 && snapshot.ForwardLossRatio >= policy?.WarningForwardLossRatio;
        snapshot = snapshot with { ActionType = reasons.Count > 0 ? null : exit ? nameof(ActionType.ExitTradePosition) : nameof(ActionType.WarnTradePosition),
            ActionState = reasons.Count > 0 ? null : exit ? nameof(ActionState.RedAlert) : warning ? nameof(ActionState.Warning) : nameof(ActionState.Normal),
            ActionReason = snapshot.ExitReason, ActionSubType = exit ? nameof(ActionSubType.ForwardLossRiskLimitReached) : nameof(ActionSubType.ForwardLossRiskLimitClear) };
        return snapshot with { UnavailableReasons = reasons.ToArray(), CalculationStatus = reasons.Count == 0 ? "Complete" : stale ? "Stale" : "Unavailable" };
    }

    /// <summary>Calculates nonnegative loss = max(0, -pnl), in currency.</summary>
    public static decimal CalculateDailyLoss(decimal pnl) => Math.Max(0m, -pnl);

    /// <summary>Calculates forward daily PnL = dailyPnl + signed scenario value change - additional exit costs.</summary>
    public static decimal CalculateForwardDailyPnl(decimal dailyPnl, decimal projectedValueChange, decimal exitCosts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exitCosts);
        return dailyPnl + projectedValueChange - exitCosts;
    }

    /// <summary>Calculates ForwardLossRatio = nonnegative forward currency loss / positive daily currency limit.</summary>
    public static double CalculateForwardLossRatio(decimal forwardLoss, decimal dailyLossLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(forwardLoss);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dailyLossLimit);
        return (double)(forwardLoss / dailyLossLimit);
    }

    /// <summary>Calculates signed OptionCalculator spread points per strategy unit = sum(legPrice * signedQuantity) / quantity.</summary>
    public static decimal? CalculateSpreadPrice(IronCondorTradePlanLegObservation[] legs, byte putCall, int quantity, bool forward)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        var pair = legs.Where(l => l.PutCall == putCall).ToArray();
        if (pair.Length != 2 || pair.Any(l => l.SignedQuantity is null || (forward ? l.ForwardPrice : l.TheoreticalPrice) is null)) return null;
        return pair.Sum(l => (forward ? l.ForwardPrice!.Value : l.TheoreticalPrice!.Value) * l.SignedQuantity!.Value) / quantity;
    }

    /// <summary>Calculates signed Greek per strategy unit = sum(legGreek * signedQuantity) / quantity; unavailable Greeks remain null.</summary>
    public static double? CalculateExposure(IronCondorTradePlanLegObservation[] legs, int quantity,
        Func<IronCondorTradePlanLegObservation, double?> selector)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (legs.Length != 4 || legs.Any(l => l.SignedQuantity is null || !Finite(selector(l)))) return null;
        return legs.Sum(l => selector(l)!.Value * l.SignedQuantity!.Value) / quantity;
    }

    /// <summary>Calculates liquidation value using bid for held longs and ask for held shorts, multiplied by signed quantity and currency multiplier.</summary>
    public static decimal? CalculateCloseValue(IronCondorTradePlanLegObservation[] legs, decimal? multiplier)
    {
        if (multiplier is null or <= 0 || legs.Length != 4 || legs.Any(l => l.SignedQuantity is null
            || l.BidPrice is null or < 0 || l.AskPrice is null or <= 0 || l.BidPrice > l.AskPrice)) return null;
        return legs.Sum(l => (l.SignedQuantity > 0 ? l.BidPrice!.Value : l.AskPrice!.Value) * l.SignedQuantity!.Value) * multiplier;
    }

    static bool Finite(double? value) => value is { } number && double.IsFinite(number);
}
