using System.Collections.Immutable;
using AppPricing = TomasAI.IFM.Application.MarketData.Pricing;
using DomainPricing = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Explicit application/domain adapter; preserves every frozen pricing field without encoded copies.</summary>
public static class CompositionSnapshotAdapter
{
    public static DomainPricing.OptionSelectionValue From(AppPricing.OptionSelectionValue x) =>
        new(x.Price, x.Delta, x.ImpliedVolatility, From(x.IvUnderlying), From(x.IvOption), x.IvCalculatedAtUtc,
            x.CalculatedAtUtc, x.ValidUntilUtc, x.PolicyVersion, x.ContextDigest);
    public static AppPricing.OptionSelectionValue To(DomainPricing.OptionSelectionValue x) =>
        new(x.Price, x.Delta, x.ImpliedVolatility, To(x.IvUnderlying), To(x.IvOption), x.IvCalculatedAtUtc,
            x.CalculatedAtUtc, x.ValidUntilUtc, x.PolicyVersion, x.ContextDigest);
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingQuote From(TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingQuote x) => new(x.ContractId, x.Bid, x.Ask, x.BidSize, x.AskSize, x.EventAtUtc, x.ReceivedAtUtc, x.Sequence, x.GenerationId);
    public static TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingQuote To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingQuote x) => new(x.ContractId, x.Bid, x.Ask, x.BidSize, x.AskSize, x.EventAtUtc, x.ReceivedAtUtc, x.Sequence, x.GenerationId);
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingCalendar From(TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingCalendar x) => new(x.Version, x.TimeZoneId, x.CoverageFrom, x.CoverageUntil, x.ValueDateRollover, x.TradingDates);
    public static TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingCalendar To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingCalendar x) => new(x.Version, x.TimeZoneId, x.CoverageFrom, x.CoverageUntil, x.ValueDateRollover, x.TradingDates);
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.TreasuryRateConversionPolicy From(TomasAI.IFM.Framework.MarketData.Contracts.TreasuryRateConversionPolicy x) => new(x.Source, x.SourceSeriesId, (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.TreasuryRateConvention)x.Convention, x.Version, x.EvidenceId);
    public static TomasAI.IFM.Framework.MarketData.Contracts.TreasuryRateConversionPolicy To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.TreasuryRateConversionPolicy x) => new(x.Source, x.SourceSeriesId, (TomasAI.IFM.Framework.MarketData.Contracts.TreasuryRateConvention)x.Convention, x.Version, x.EvidenceId);
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.TreasuryContinuousRate From(TomasAI.IFM.Framework.MarketData.Contracts.TreasuryContinuousRate x) => new((TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.TreasuryTenor)x.Tenor, x.RatePercent, x.AnnualContinuousRate, x.ValueDate, x.ObservedAtUtc, x.CurveDigest, From(x.Conversion), x.ModelingPolicy);
    public static TomasAI.IFM.Framework.MarketData.Contracts.TreasuryContinuousRate To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.TreasuryContinuousRate x) => new((TomasAI.IFM.Framework.MarketData.Contracts.TreasuryTenor)x.Tenor, x.RatePercent, x.AnnualContinuousRate, x.ValueDate, x.ObservedAtUtc, x.CurveDigest, To(x.Conversion), x.ModelingPolicy);
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingContext From(TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingContext x) => new(From(x.Contract), From(x.Calendar), From(x.Rate), x.ValidUntilUtc, x.GenerationId, x.PricerVersion, x.MaximumQuoteAgeMilliseconds, x.MaximumQuoteSkewMilliseconds, x.PublicationPolicyVersion);
    public static TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingContext To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingContext x) => new(To(x.Contract), To(x.Calendar), To(x.Rate), x.ValidUntilUtc, x.GenerationId, x.PricerVersion, x.MaximumQuoteAgeMilliseconds, x.MaximumQuoteSkewMilliseconds, x.PublicationPolicyVersion);
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.CompositionFutureDefinition From(TomasAI.IFM.Application.MarketData.Pricing.CompositionFutureDefinition x) => new(x.ContractId, x.Root, x.Dataset, x.Exchange, x.Currency, x.LastTradingUtc, x.Multiplier, x.TickSize, x.DefinitionDigest);
    public static TomasAI.IFM.Application.MarketData.Pricing.CompositionFutureDefinition To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.CompositionFutureDefinition x) => new(x.ContractId, x.Root, x.Dataset, x.Exchange, x.Currency, x.LastTradingUtc, x.Multiplier, x.TickSize, x.DefinitionDigest);
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingValue From(TomasAI.IFM.Application.MarketData.Pricing.OptionPricingValue x) => new(x.ImpliedVolatility, x.Delta, x.Gamma, x.Theta, x.Vega, x.Rho, x.TheoreticalPrice, x.TimeToExpiry, x.ContextDigest);
    public static TomasAI.IFM.Application.MarketData.Pricing.OptionPricingValue To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingValue x) => new(x.ImpliedVolatility, x.Delta, x.Gamma, x.Theta, x.Vega, x.Rho, x.TheoreticalPrice, x.TimeToExpiry, x.ContextDigest);
    public static DomainPricing.CompositionMarketInstrument From(AppPricing.CompositionMarketInstrument x) => new(x.ContractId, From(x.Quote), x.Pricing is null ? null : From(x.Pricing), x.Strike, x.IsCall, x.Underlying is null ? null : From(x.Underlying), x.FutureDefinition is null ? null : From(x.FutureDefinition))
        { Selection = x.Selection is null ? null : From(x.Selection) };
    public static AppPricing.CompositionMarketInstrument To(DomainPricing.CompositionMarketInstrument x) => new(x.ContractId, To(x.Quote), x.Pricing is null ? null : To(x.Pricing), x.Strike, x.IsCall, x.Underlying is null ? null : To(x.Underlying), x.FutureDefinition is null ? null : To(x.FutureDefinition))
        { Selection = x.Selection is null ? null : To(x.Selection) };
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.CompositionInstrumentSnapshot From(TomasAI.IFM.Application.MarketData.Pricing.CompositionInstrumentSnapshot x) => new(From(x.Instrument), x.Valuation is null ? null : From(x.Valuation));
    public static TomasAI.IFM.Application.MarketData.Pricing.CompositionInstrumentSnapshot To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.CompositionInstrumentSnapshot x) => new(To(x.Instrument), x.Valuation is null ? null : To(x.Valuation));
    /// <summary>Converts an application snapshot and seals the digest for its domain pricing representation.</summary>
    /// <param name="x">The application pricing snapshot to convert.</param>
    /// <returns>The converted domain snapshot with its representation-specific semantic digest.</returns>
    public static DomainPricing.MarketCompositionSnapshot From(AppPricing.MarketCompositionSnapshot x)
    {
        var converted = new DomainPricing.MarketCompositionSnapshot(x.SchemaVersion, x.SnapshotId, x.ScopeId,
            x.ScopeToken, x.Horizon, x.GenerationId, x.EvaluatedAtUtc, x.ValidUntilUtc,
            x.Instruments.Select(From).ToImmutableArray(), "");
        return converted with { Digest = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionSemanticHash.Compute(converted) };
    }
    /// <summary>Converts a domain snapshot and seals the digest for its application pricing representation.</summary>
    /// <param name="x">The domain pricing snapshot to convert.</param>
    /// <returns>The converted application snapshot with its representation-specific semantic digest.</returns>
    public static AppPricing.MarketCompositionSnapshot To(DomainPricing.MarketCompositionSnapshot x)
    {
        var converted = new AppPricing.MarketCompositionSnapshot(x.SchemaVersion, x.SnapshotId, x.ScopeId,
            x.ScopeToken, x.Horizon, x.GenerationId, x.EvaluatedAtUtc, x.ValidUntilUtc,
            x.Instruments.Select(To).ToImmutableArray(), "");
        return converted with { Digest = AppPricing.PricingSemanticHash.Compute(converted) };
    }
    public static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingConvention From(TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingConvention x) => new()
    {
        SchemaVersion = x.SchemaVersion,
        ContractId = x.ContractId,
        Dataset = x.Dataset,
        PublisherId = x.PublisherId,
        InstrumentId = x.InstrumentId,
        RawSymbol = x.RawSymbol,
        Root = x.Root,
        Exchange = x.Exchange,
        Currency = x.Currency,
        UnderlyingContractId = x.UnderlyingContractId,
        ExerciseStyle = (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionExerciseStyle)x.ExerciseStyle,
        SettlementStyle = (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionSettlementStyle)x.SettlementStyle,
        ExpirationUtc = x.ExpirationUtc,
        LastTradingUtc = x.LastTradingUtc,
        DayCount = (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.PricingDayCount)x.DayCount,
        CalendarVersion = x.CalendarVersion,
        Multiplier = x.Multiplier,
        TickSize = x.TickSize,
        TickRuleVersion = x.TickRuleVersion,
        DefinitionDigest = x.DefinitionDigest,
        MappingVersion = x.MappingVersion,
        EvidenceId = x.EvidenceId,
        EffectiveFromUtc = x.EffectiveFromUtc,
        EffectiveUntilUtc = x.EffectiveUntilUtc,
        PremiumTickRule = (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPremiumTickRule)x.PremiumTickRule,
        PremiumStyle = (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPremiumStyle)x.PremiumStyle,
        UnderlyingKind = (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.PricingUnderlyingKind)x.UnderlyingKind,
        Strike = x.Strike,
        Right = (TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.PricingOptionRight)x.Right,
    };
    public static TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPricingConvention To(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingConvention x) => new()
    {
        SchemaVersion = x.SchemaVersion,
        ContractId = x.ContractId,
        Dataset = x.Dataset,
        PublisherId = x.PublisherId,
        InstrumentId = x.InstrumentId,
        RawSymbol = x.RawSymbol,
        Root = x.Root,
        Exchange = x.Exchange,
        Currency = x.Currency,
        UnderlyingContractId = x.UnderlyingContractId,
        ExerciseStyle = (TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionExerciseStyle)x.ExerciseStyle,
        SettlementStyle = (TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionSettlementStyle)x.SettlementStyle,
        ExpirationUtc = x.ExpirationUtc,
        LastTradingUtc = x.LastTradingUtc,
        DayCount = (TomasAI.IFM.Framework.MarketData.Contracts.Pricing.PricingDayCount)x.DayCount,
        CalendarVersion = x.CalendarVersion,
        Multiplier = x.Multiplier,
        TickSize = x.TickSize,
        TickRuleVersion = x.TickRuleVersion,
        DefinitionDigest = x.DefinitionDigest,
        MappingVersion = x.MappingVersion,
        EvidenceId = x.EvidenceId,
        EffectiveFromUtc = x.EffectiveFromUtc,
        EffectiveUntilUtc = x.EffectiveUntilUtc,
        PremiumTickRule = (TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPremiumTickRule)x.PremiumTickRule,
        PremiumStyle = (TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionPremiumStyle)x.PremiumStyle,
        UnderlyingKind = (TomasAI.IFM.Framework.MarketData.Contracts.Pricing.PricingUnderlyingKind)x.UnderlyingKind,
        Strike = x.Strike,
        Right = (TomasAI.IFM.Framework.MarketData.Contracts.Pricing.PricingOptionRight)x.Right,
    };
}
