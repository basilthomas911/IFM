using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Applies explicit review evidence to the exact provider contract before SecuritiesDb publication.</summary>
public static class ReviewedOptionReference
{
    /// <summary>Combines provider identity and strike with reviewed pricing terms. No independent convention is persisted.</summary>
    /// <param name="definition">The exact provider definition, including its underlying provider identity and evidence.</param>
    /// <param name="convention">The explicitly reviewed economic terms for this definition.</param>
    /// <returns>A qualified contract whose pricing convention can be derived deterministically.</returns>
    public static FuturesOptionContractReadModel Apply(FuturesOptionContractReadModel definition, OptionPricingConvention convention)
    {
        ArgumentNullException.ThrowIfNull(convention);
        ArgumentNullException.ThrowIfNull(definition);
        if (!string.Equals(definition.ContractId,convention.ContractId,StringComparison.Ordinal) || !string.Equals(definition.Dataset,convention.Dataset,StringComparison.Ordinal)
            || definition.PublisherId != convention.PublisherId || definition.InstrumentId != convention.InstrumentId
            || !string.Equals(definition.RawSymbol,convention.RawSymbol,StringComparison.Ordinal) || !string.Equals(definition.DefinitionDigest,convention.DefinitionDigest,StringComparison.Ordinal)
            || !string.Equals(definition.UnderlyingContractId,convention.UnderlyingContractId,StringComparison.Ordinal)
            || !string.Equals(definition.Exchange,convention.Exchange,StringComparison.Ordinal) || !string.Equals(definition.Currency,convention.Currency,StringComparison.Ordinal)
            || convention.SchemaVersion >= 3 && (convention.Strike != definition.GetExactStrikePrice()
                || convention.Right != (PricingOptionRight)definition.OptionRight
                || convention.UnderlyingKind != PricingUnderlyingKind.Futures))
            throw new InvalidDataException("Reviewed convention differs from the exact provider contract identity/evidence.");
        var result = definition with
        {
            SchemaVersion = 1,
            ReviewState = ReferenceReviewState.Reviewed,
            ExpirationUtc = convention.ExpirationUtc,
            LastTradingUtc = convention.LastTradingUtc,
            SettlementStyle = (ReferenceSettlementStyle)convention.SettlementStyle,
            ExerciseStyle = (ReferenceExerciseStyle)convention.ExerciseStyle,
            PremiumStyle = convention.SchemaVersion >= 3
                ? (ReferencePremiumStyle)convention.PremiumStyle : definition.PremiumStyle,
            PremiumTickRule = convention.PremiumTickRule == OptionPremiumTickRule.Unspecified
                ? ReferencePremiumTickRule.Fixed : (ReferencePremiumTickRule)convention.PremiumTickRule,
            TickRuleVersion = convention.TickRuleVersion,
            DayCount = (ReferenceDayCount)convention.DayCount,
            MultiplierValue = convention.Multiplier,
            Multiplier = convention.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TickSize = convention.TickSize,
            CalendarVersion = convention.CalendarVersion,
            MappingVersion = convention.MappingVersion,
            EvidenceId = convention.EvidenceId,
            EffectiveFromUtc = convention.EffectiveFromUtc,
            EffectiveUntilUtc = convention.EffectiveUntilUtc
        };
        _ = ReviewedOptionConvention.From(result);
        return result;
    }
}
