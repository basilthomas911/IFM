using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Defines the complete, reproducible calculation contract for a MACD signal.
/// </summary>
[MessagePackObject]
public sealed record FuturesMacdConfiguration
{
    public const int ConventionalSignalEmaPeriod = 9;
    public const int ConventionalFastEmaPeriod = 12;
    public const int ConventionalSlowEmaPeriod = 26;

    [Key(0)] public int SignalEmaPeriod { get; init; } = ConventionalSignalEmaPeriod;
    [Key(1)] public int FastEmaPeriod { get; init; } = ConventionalFastEmaPeriod;
    [Key(2)] public int SlowEmaPeriod { get; init; } = ConventionalSlowEmaPeriod;

    public static FuturesMacdConfiguration Standard { get; } = new();

    public FuturesMacdConfiguration() { }

    [SerializationConstructor]
    public FuturesMacdConfiguration(
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod)
    {
        SignalEmaPeriod = signalEmaPeriod;
        FastEmaPeriod = fastEmaPeriod;
        SlowEmaPeriod = slowEmaPeriod;
    }

    public string Format() => $"{SignalEmaPeriod}.{FastEmaPeriod}.{SlowEmaPeriod}";
}

/// <summary>Validates a MACD calculation configuration.</summary>
public sealed class FuturesMacdConfigurationValidationRules
    : BaseValidationRules, IValidationRules<FuturesMacdConfiguration>
{
    static readonly Validator Rules = new();

    public ValidationError[] Execute(FuturesMacdConfiguration configuration)
        => Validate(configuration, Rules);

    sealed class Validator : AbstractValidator<FuturesMacdConfiguration>
    {
        public Validator()
        {
            RuleFor(x => x.SignalEmaPeriod).GreaterThan(0);
            RuleFor(x => x.FastEmaPeriod).GreaterThan(0);
            RuleFor(x => x.SlowEmaPeriod).GreaterThan(0);
            RuleFor(x => x).Must(x => x.FastEmaPeriod < x.SlowEmaPeriod)
                .WithMessage("MACD FastEmaPeriod must be less than SlowEmaPeriod");
        }

        public override ValidationResult Validate(ValidationContext<FuturesMacdConfiguration> context)
        {
            if (context.InstanceToValidate is null)
                return new ValidationResult([
                    new ValidationFailure(nameof(FuturesMacdConfiguration), "MACD configuration is null")
                ]);
            return base.Validate(context);
        }
    }
}
