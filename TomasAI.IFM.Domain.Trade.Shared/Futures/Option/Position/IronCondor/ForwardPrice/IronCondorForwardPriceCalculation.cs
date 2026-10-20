using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

/// <summary>Trade-owned pure implementation of the original delta/time adjustment. No OptionPricer domain dependency.</summary>
public static class IronCondorForwardPriceCalculation
{
    /// <summary>The fixed strength applied to the short-option delta and remaining-lifetime adjustment.</summary>
    public const double AmplificationStrength = 0.20;

    /// <summary>Applies the spread mean/fallback and delta/time skew using the fixed amplification strength.</summary>
    public static double AdjustSpread(double mean, double currentMagnitude, double shortDelta, int remainingDays, int openingDays, bool riskSide)
    {
        if (!double.IsFinite(mean) || !double.IsFinite(currentMagnitude) || currentMagnitude < 0
            || !double.IsFinite(shortDelta) || Math.Abs(shortDelta) > 1 || remainingDays <= 0 || openingDays <= 0)
            throw new ArgumentException("IronCondorForwardPrice.INPUTS.INVALID: finite prices/delta and positive calendar day counts required.");
        var value = (mean > 0 ? mean : currentMagnitude) * (1 + (riskSide ? 1 : -1) * AmplificationStrength * Math.Abs(shortDelta) * Math.Sqrt((double)remainingDays / openingDays));
        if (!double.IsFinite(value)) throw new ArgumentException("IronCondorForwardPrice.PRICE.NONFINITE");
        return value;
    }

    /// <summary>Calculates raw put plus call forward prices; position direction never forces a negative result.</summary>
    public static IronCondorForwardPrice Calculate(CalculateIronCondorForwardPriceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.IronCondorForwardPriceInputs);
        ArgumentNullException.ThrowIfNull(command.Position);
        var inputs = command.IronCondorForwardPriceInputs;
        ArgumentNullException.ThrowIfNull(inputs.OptionLegs);
        ArgumentNullException.ThrowIfNull(command.Position.Legs);
        var legs = inputs.OptionLegs;
        var position = command.Position;
        if (legs.Length != 4 || position.Legs.Length != 4 || !command.EntityId.IsValid || command.EntityId.Position != position.Id
            || legs.Any(l => l.SignedQuantity is 0 or int.MinValue || l.ContractMultiplier <= 0 || string.IsNullOrWhiteSpace(l.Currency)
                || !double.IsFinite(l.TheoreticalPrice) || l.TheoreticalPrice < 0 || !double.IsFinite(l.Delta) || Math.Abs(l.Delta) > 1 || l.MarkPrice < 0)
            || legs.Select(l => l.TradeLegId).Distinct().Count() != 4 || legs.Select(l => l.ContractId).Distinct(StringComparer.Ordinal).Count() != 4
            || legs.Select(l => Math.Abs(l.SignedQuantity)).Distinct().Count() != 1
            || legs.Select(l => l.ContractMultiplier).Distinct().Count() != 1 || legs.Select(l => l.Currency).Distinct(StringComparer.Ordinal).Count() != 1
            || legs.Any(l => !position.Legs.Any(p => p.TradeLegId == l.TradeLegId && p.ContractId == l.ContractId && p.SignedQuantity == l.SignedQuantity && p.PutCall == (l.IsCall ? 1 : 2))))
            throw new ArgumentException("IronCondorForwardPrice.CONTRACTS.INVALID: four exact balanced legs with matching actual currency/multiplier required.");
        var put = legs.Where(l => !l.IsCall).ToArray();
        var call = legs.Where(l => l.IsCall).ToArray();
        if (put.Length != 2 || call.Length != 2 || put.Sum(l => l.SignedQuantity) != 0 || call.Sum(l => l.SignedQuantity) != 0)
            throw new ArgumentException("IronCondorForwardPrice.STRATEGY.UNBALANCED");
        var putShort = put.Single(l => l.SignedQuantity < 0);
        var callShort = call.Single(l => l.SignedQuantity < 0);
        var remaining = inputs.MaturityDate.DayNumber - command.EntityId.ValueDate.DayNumber;
        var total = inputs.MaturityDate.DayNumber - inputs.TradeDate.DayNumber;
        // Legacy OTM = 1 - abs(short delta). On a tie put is selected as the risk side.
        var putRisk = 1 - Math.Abs(putShort.Delta) <= 1 - Math.Abs(callShort.Delta);
        double Forward(IronCondorForwardPriceLeg[] pair, bool risk) => AdjustSpread(
            pair.Single(l => l.SignedQuantity < 0).TheoreticalPrice - pair.Single(l => l.SignedQuantity > 0).TheoreticalPrice,
            (double)Math.Abs(pair.Sum(l => Math.Sign(l.SignedQuantity) * l.MarkPrice)),
            pair.Single(l => l.SignedQuantity < 0).Delta, remaining, total, risk);
        var putForward = checked((decimal)Forward(put, putRisk));
        var callForward = checked((decimal)Forward(call, !putRisk));
        return new()
        {
            ForwardPrice = putForward + callForward, PutForwardPricePoints = putForward, CallForwardPricePoints = callForward,
            ContractMultiplier = legs[0].ContractMultiplier, Currency = legs[0].Currency, StrategyQuantity = Math.Abs(legs[0].SignedQuantity),
            PositionSession = command.EntityId, PositionSequence = position.PositionSequence, RouteGeneration = position.RouteGeneration,
            SourceEventId = command.SourceEventId, CalculatedAtUtc = command.RequestedAtUtc, InputFingerprint = command.Fingerprint(), IronCondorForwardPriceInputs = inputs,
            ModelVersion = "IronCondor/DeltaTimeStrength020RawSum/v2"
        };
    }

    /// <summary>Subtracts projected closing cost from entry credit, scales once and evaluates the whole-trade loss budget.</summary>
    /// <param name="completed">The successful raw projected closing-cost result and actual contract conventions.</param>
    /// <param name="entryCreditPoints">Positive entry credit per complete strategy unit, captured from opening leg prices.</param>
    /// <param name="maxLossLimit">The positive whole-trade loss budget in the same contract currency.</param>
    /// <returns>Versioned projected PnL and signed loss; the amplified price is neither capped nor sign-flipped.</returns>
    public static IronCondorForwardRisk CalculateRisk(IronCondorForwardPriceCalculatedEvent completed, decimal entryCreditPoints, decimal maxLossLimit)
    {
        ArgumentNullException.ThrowIfNull(completed);
        var price = completed.IronCondorForwardPrice;
        if (entryCreditPoints <= 0 || maxLossLimit <= 0 || price.ContractMultiplier <= 0 || price.StrategyQuantity <= 0 || string.IsNullOrWhiteSpace(price.Currency))
            throw new ArgumentException("IronCondorForwardPrice.LIMIT.INVALID: positive entry credit, actual contract conventions and trade currency loss limit required.");
        var forwardPnl = checked((entryCreditPoints - price.ForwardPrice) * price.ContractMultiplier * price.StrategyQuantity);
        var loss = Math.Min(0m, forwardPnl);
        return new() { IronCondorForwardPrice = price, ForwardLoss = loss, MaxLossLimit = maxLossLimit,
            ForwardLossRatio = loss < 0 ? (double)(loss / -maxLossLimit) : 0,
            ForwardLossHeadroom = maxLossLimit + Math.Min(loss, 0), ExitRecommended = loss <= -maxLossLimit,
            ForwardPriceCommandId = completed.CommandId, EntryCreditPoints = entryCreditPoints, ForwardPnl = forwardPnl,
            CalculationVersion = IronCondorForwardRisk.EntryCreditCalculationVersion };
    }
}
