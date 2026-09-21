using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Framework.MarketData.Pricing;

/// <summary>The pricing convention is derived from the same reviewed reference version, never independently edited.</summary>
public static class ReviewedOptionConvention
{
    public static OptionPricingConvention From(FuturesOptionContractReadModel value)
    {
        var errors = FuturesReferenceQualification.Errors(value);
        if (errors.Count != 0) throw new ArgumentException("Unqualified option reference: " + string.Join(", ", errors));
        var result = new OptionPricingConvention
        {
            SchemaVersion = 3, ContractId = value.ContractId, Dataset = value.Dataset!, PublisherId = value.PublisherId!.Value,
            InstrumentId = value.InstrumentId!.Value, RawSymbol = value.RawSymbol!, Root = value.Symbol,
            Exchange = value.Exchange, Currency = value.Currency, UnderlyingContractId = value.UnderlyingContractId!,
            ExerciseStyle = (OptionExerciseStyle)value.ExerciseStyle, SettlementStyle = (OptionSettlementStyle)value.SettlementStyle,
            ExpirationUtc = value.ExpirationUtc!.Value, LastTradingUtc = value.LastTradingUtc!.Value,
            DayCount = (PricingDayCount)value.DayCount, CalendarVersion = value.CalendarVersion!,
            Multiplier = value.MultiplierValue!.Value, TickSize = value.TickSize!.Value,
            TickRuleVersion = value.TickRuleVersion!, DefinitionDigest = value.DefinitionDigest!,
            MappingVersion = value.MappingVersion!, EvidenceId = value.EvidenceId!,
            EffectiveFromUtc = value.EffectiveFromUtc!.Value, EffectiveUntilUtc = value.EffectiveUntilUtc!.Value,
            PremiumTickRule = (OptionPremiumTickRule)value.PremiumTickRule, PremiumStyle = (OptionPremiumStyle)value.PremiumStyle,
            UnderlyingKind = PricingUnderlyingKind.Futures, Strike = value.GetExactStrikePrice(), Right = (PricingOptionRight)value.OptionRight
        };
        var invalid = OptionPricingQualification.Validate(result, result.EffectiveFromUtc);
        if (invalid is not null) throw new ArgumentException(invalid.Code);
        return result;
    }
}
