using FluentValidation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;

/// <summary>Validates RSI configuration identity, value bounds, and warm/slope consistency.</summary>
public sealed class FuturesRegimeRsiSignalReadModelValidationRules
    : BaseValidationRules, IValidationRules<FuturesRegimeRsiSignalReadModel>
{
    static readonly Validator Rules = new();

    /// <summary>Validates one RSI signal.</summary>
    public ValidationError[] Execute(FuturesRegimeRsiSignalReadModel value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesRegimeRsiSignalReadModel>
    {
        public Validator()
        {
            RuleFor(x => x.Metadata).Must(ValidMetadata);
            RuleFor(x => x.Period).GreaterThan(0);
            RuleFor(x => x.Value).Must(Bounded).When(x => x.Value is not null);
            RuleFor(x => x.PreviousValue).Must(Bounded).When(x => x.PreviousValue is not null);
            RuleFor(x => x).Must(x => x.IsWarm ==
                (x.Value is not null && x.PreviousValue is not null && x.Slope is not null));
        }

        static bool Bounded(double? value) => value is >= 0 and <= 100;
    }

    internal static bool ValidMetadata(MarketAnalyticsSignalMetadata metadata) =>
        metadata is not null
        && new MarketAnalyticsSignalMetadataValidationRules().Execute(metadata).Length == 0;
}

/// <summary>Validates EMA lineage and exact warm-state semantics.</summary>
public sealed class FuturesEmaSignalReadModelValidationRules
    : BaseValidationRules, IValidationRules<FuturesEmaSignalReadModel>
{
    static readonly Validator Rules = new();

    /// <summary>Validates one EMA family signal.</summary>
    public ValidationError[] Execute(FuturesEmaSignalReadModel value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesEmaSignalReadModel>
    {
        public Validator()
        {
            RuleFor(x => x.Metadata).Must(FuturesRegimeRsiSignalReadModelValidationRules.ValidMetadata);
            RuleFor(x => x.Price).GreaterThan(0);
            RuleFor(x => x).Must(x => x.IsWarm ==
                (x.Ema200 is not null && x.PreviousEma200 is not null && x.Ema200Slope is not null));
        }
    }
}

/// <summary>Validates Bollinger identity, bands, widths, ratios, and warm state.</summary>
public sealed class FuturesBollingerBandSignalReadModelValidationRules
    : BaseValidationRules, IValidationRules<FuturesBollingerBandSignalReadModel>
{
    static readonly Validator Rules = new();

    /// <summary>Validates one Bollinger signal.</summary>
    public ValidationError[] Execute(FuturesBollingerBandSignalReadModel value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesBollingerBandSignalReadModel>
    {
        public Validator()
        {
            RuleFor(x => x.Metadata).Must(FuturesRegimeRsiSignalReadModelValidationRules.ValidMetadata);
            RuleFor(x => x.Price).GreaterThan(0);
            RuleFor(x => x).Must(x => x.Width10 is null || x.Width10 >= 0);
            RuleFor(x => x).Must(x => x.Width20 is null || x.Width20 >= 0);
            RuleFor(x => x).Must(x => x.IsWarm ==
                (x.Width20 is > 0 && x.Width20Baseline is > 0 && x.Width20Ratio is not null));
        }
    }
}
