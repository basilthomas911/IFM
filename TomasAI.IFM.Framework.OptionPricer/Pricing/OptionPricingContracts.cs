using System.Collections.Immutable;

namespace TomasAI.IFM.Framework.OptionPricer.Pricing;

public enum UnderlyingKind { Unknown, Futures, Equity }
public enum ExerciseKind { Unknown, European, American }
public enum PremiumKind { Unknown, PaidUpfront, FuturesStyle }
public enum OptionSide { Unknown, Call, Put }
public enum DividendKind { Unknown, None, ContinuousYield, DiscreteCash }
/// <summary>Known cash dividend per share, at a qualified year fraction after valuation.</summary>
public readonly record struct CashDividend(double Time, double Amount);
public enum PricingFailure
{
    None, InvalidInput, UnsupportedConvention, Expired, UndefinedGreeks,
    PriceOutOfBounds, ImpliedVolatilityNotIdentifiable, VolatilityNotBracketed,
    NonConvergence, NumericalFailure
}

/// <summary>Annual decimal units. Prices and Greeks are per option, before quantities/multipliers.
/// TimeToExpiry is the caller's qualified year fraction; no machine clock is consulted.</summary>
public readonly record struct OptionPricingRequest(
    UnderlyingKind Underlying, ExerciseKind Exercise, PremiumKind Premium,
    OptionSide Side, double UnderlyingPrice, double Strike, double TimeToExpiry,
    double Rate, DividendKind Dividends = DividendKind.None, double DividendYield = 0)
{
    public ImmutableArray<CashDividend> CashDividends { get; init; } = [];
}

/// <summary>Bounded numerical policy, included in result provenance.</summary>
public sealed record PricingSettings
{
    public int Steps { get; init; } = 801;
    public int MaximumIterations { get; init; } = 100;
    public double MaximumVolatility { get; init; } = 4;
    public double PriceTolerance { get; init; } = 1e-8;
    public int SpatialSteps { get; init; } = 800;
    public int MaximumSorIterations { get; init; } = 2000;
    public double SorTolerance { get; init; } = 1e-9;
    public const string Version = "PricingPolicy/v2";
}

public readonly record struct OptionValues(
    double Price, double Volatility, double Delta, double Gamma,
    double Vega, double Theta, double Rho);

/// <summary>Only calculated outputs; no placeholders for unrequested Greeks.</summary>
public readonly record struct OptionPriceDeltaValues(double Price, double Volatility, double Delta);

public readonly record struct PriceDeltaResult(
    OptionPriceDeltaValues? Value, PricingFailure Failure, string EngineVersion, int Steps)
{
    public bool Success => Failure == PricingFailure.None && Value.HasValue;
    public string PolicyVersion => PricingSettings.Version;
    public OptionPricingRequest Request { get; init; }
    public PricingSettings? NumericalPolicy { get; init; }
}

/// <summary>Residual is repriced price minus market price. Iterations counts bisection iterations.</summary>
public readonly record struct ImpliedVolatilityValues(
    double Volatility, double RepricedPrice, double Residual, int Iterations);

public readonly record struct ImpliedVolatilityResult(
    ImpliedVolatilityValues? Value, PricingFailure Failure, string EngineVersion, int Steps)
{
    public bool Success => Failure == PricingFailure.None && Value.HasValue;
    public string PolicyVersion => PricingSettings.Version;
    public OptionPricingRequest Request { get; init; }
    public PricingSettings? NumericalPolicy { get; init; }
}

public readonly record struct OptionPriceResult(double? Price, PricingFailure Failure, string EngineVersion)
{
    public bool Success => Failure == PricingFailure.None && Price.HasValue;
}

/// <summary>Failure has no numerical payload. Theta is per year, Vega/Rho per unit decimal.</summary>
public readonly record struct PricingResult(
    OptionValues? Value, PricingFailure Failure, string EngineVersion, int Steps)
{
    public bool Success => Failure == PricingFailure.None && Value.HasValue;
    public string PolicyVersion => PricingSettings.Version;
    public OptionPricingRequest Request { get; init; }
    public PricingSettings? NumericalPolicy { get; init; }
}
