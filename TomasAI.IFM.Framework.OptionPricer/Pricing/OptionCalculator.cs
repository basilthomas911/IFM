using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Framework.OptionPricer.Black76;
using TomasAI.IFM.Framework.OptionPricer.Interop;

namespace TomasAI.IFM.Framework.OptionPricer.Pricing;

/// <summary>Model-neutral pricing boundary for futures and equity option contracts.</summary>
public sealed class OptionCalculator
{
    public const string Version = "UnifiedOptionCalculator/v1";
    private const string EuropeanEquityEngine = "BlackScholesMerton.Managed/v1";
    private readonly PricingSettings settings;
    public OptionCalculator(PricingSettings? settings = null)
    {
        this.settings = settings ?? new();
        if (this.settings.Steps is < 32 or > 4096 ||
            this.settings.MaximumIterations is < 1 or > 256 ||
            !double.IsFinite(this.settings.MaximumVolatility) || this.settings.MaximumVolatility is <= 0 or > 10 ||
            !double.IsFinite(this.settings.PriceTolerance) || this.settings.PriceTolerance is <= 0 or > .01 ||
            this.settings.SpatialSteps is < 64 or > 4096 ||
            this.settings.MaximumSorIterations is < 1 or > 10000 ||
            !double.IsFinite(this.settings.SorTolerance) || this.settings.SorTolerance is <= 0 or > 1e-4)
            throw new ArgumentOutOfRangeException(nameof(settings));
    }

    /// <summary>Returns the concrete algorithm selected for the supplied contract conventions.</summary>
    public static string EngineVersionFor(in OptionPricingRequest request) => Engine(request);

    /// <summary>Price-only API includes zero-volatility and expiry values where smooth Greeks may not exist.</summary>
    public OptionPriceResult TheoreticalPrice(in OptionPricingRequest request, double volatility,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = Validate(request);
        if (failure != PricingFailure.None) return new(null, failure, Engine(request));
        if (!double.IsFinite(volatility) || volatility < 0 || volatility > settings.MaximumVolatility)
            return new(null, PricingFailure.InvalidInput, Engine(request));
        double price;
        if (request.TimeToExpiry == 0)
            price = Math.Max(Sign(request) * (request.UnderlyingPrice - request.Strike), 0);
        else if (volatility == 0)
        {
            if (request.Dividends == DividendKind.DiscreteCash)
                return new(CashDividendModel.Deterministic(request), PricingFailure.None, Engine(request));
            var r = request;
            var discountRate = r.Premium == PremiumKind.FuturesStyle ? 0 : r.Rate;
            var carry = r.Underlying == UnderlyingKind.Equity ? r.Rate - r.DividendYield : 0;
            double Payoff(double t) => Math.Exp(-discountRate * t) *
                Math.Max(Sign(r) * (r.UnderlyingPrice * Math.Exp(carry * t) - r.Strike), 0);
            price = Payoff(r.TimeToExpiry);
            if (r.Exercise == ExerciseKind.American)
            {
                price = Math.Max(price, Payoff(0));
                var ratio = -discountRate * r.Strike / ((carry - discountRate) * r.UnderlyingPrice);
                if (carry != 0 && ratio > 0 && double.IsFinite(ratio))
                {
                    var critical = Math.Log(ratio) / carry;
                    if (critical > 0 && critical < r.TimeToExpiry) price = Math.Max(price, Payoff(critical));
                }
            }
        }
        else price = Evaluate(request, volatility, cancellationToken).Price;
        return double.IsFinite(price) ? new(price, PricingFailure.None, Engine(request)) :
            new(null, PricingFailure.NumericalFailure, Engine(request));
    }

    /// <summary>One core numerical evaluation, without Vega/Rho/Theta repricing or IV solving.</summary>
    public PriceDeltaResult PriceAndDelta(in OptionPricingRequest request, double volatility,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = Validate(request);
        if (failure != PricingFailure.None) return FailDelta(request, failure);
        if (!double.IsFinite(volatility) || volatility < 0 || volatility > settings.MaximumVolatility)
            return FailDelta(request, PricingFailure.InvalidInput);
        if (request.TimeToExpiry == 0 || volatility == 0)
            return FailDelta(request, PricingFailure.UndefinedGreeks);
        double price, delta;
        if (request.Exercise == ExerciseKind.European && request.Dividends != DividendKind.DiscreteCash)
        {
            bool equity = request.Underlying == UnderlyingKind.Equity;
            double forward = equity ? request.UnderlyingPrice *
                Math.Exp((request.Rate - request.DividendYield) * request.TimeToExpiry) : request.UnderlyingPrice;
            double rate = request.Premium == PremiumKind.FuturesStyle ? 0 : request.Rate;
            var value = equity
                ? OptionModel.PriceAndDeltaManaged(forward, request.Strike, rate, volatility, request.TimeToExpiry, Sign(request))
                : OptionModel.PriceAndDelta(forward, request.Strike, rate, volatility, request.TimeToExpiry, Sign(request));
            price = value.Price;
            delta = value.Delta * (forward / request.UnderlyingPrice);
        }
        else
        {
            var core = Evaluate(request, volatility, cancellationToken);
            price = core.Price;
            delta = core.Delta;
        }
        return double.IsFinite(price) && double.IsFinite(delta)
            ? new(new(price, volatility, delta), PricingFailure.None, Engine(request), settings.Steps)
                { Request = request, NumericalPolicy = settings }
            : FailDelta(request, PricingFailure.NumericalFailure);
    }

    public PricingResult Price(in OptionPricingRequest request, double volatility, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = Validate(request);
        if (failure != PricingFailure.None) return Fail(request, failure);
        if (!double.IsFinite(volatility) || volatility < 0 || volatility > settings.MaximumVolatility)
            return Fail(request, PricingFailure.InvalidInput);
        // The price exists at these boundaries, but a complete smooth Greek vector does not.
        if (request.TimeToExpiry == 0 || volatility == 0)
            return Fail(request, PricingFailure.UndefinedGreeks);
        if (request.Exercise == ExerciseKind.European && request.Dividends != DividendKind.DiscreteCash)
        {
            var forward = request.Underlying == UnderlyingKind.Futures ? request.UnderlyingPrice :
                request.UnderlyingPrice * Math.Exp((request.Rate - request.DividendYield) * request.TimeToExpiry);
            var rate = request.Premium == PremiumKind.FuturesStyle ? 0 : request.Rate;
            var g = request.Underlying == UnderlyingKind.Equity
                ? OptionModel.PriceWithGreeksManaged(forward, request.Strike, rate, volatility, request.TimeToExpiry, Sign(request))
                : OptionModel.PriceWithGreeks(forward, request.Strike, rate, volatility, request.TimeToExpiry, Sign(request));
            var factor = forward / request.UnderlyingPrice;
            var equity = request.Underlying == UnderlyingKind.Equity;
            return Complete(request, new(g.Price, volatility, g.Delta * factor, g.Gamma * factor * factor,
                g.Vega, equity ? g.Theta - (request.Rate - request.DividendYield) * forward * g.Delta : g.Theta,
                equity ? g.Rho + request.TimeToExpiry * forward * g.Delta :
                    request.Premium == PremiumKind.FuturesStyle ? 0 : g.Rho));
        }
        var core = Evaluate(request, volatility, cancellationToken);
        if (!double.IsFinite(core.Price)) return Fail(request, PricingFailure.NumericalFailure);
        var dv = Math.Min(1e-4, volatility / 2);
        const double dr = 1e-5;
        var dt = Math.Min(1e-5, request.TimeToExpiry / 2);
        if (request.Dividends == DividendKind.DiscreteCash && request.CashDividends.Length > 0)
            dt = Math.Min(dt, request.CashDividends[0].Time / 2);
        var vega = (Evaluate(request, volatility + dv, cancellationToken).Price -
            Evaluate(request, volatility - dv, cancellationToken).Price) / (2 * dv);
        var rho = (Evaluate(request with { Rate = request.Rate + dr }, volatility, cancellationToken).Price -
            Evaluate(request with { Rate = request.Rate - dr }, volatility, cancellationToken).Price) / (2 * dr);
        // Calendar theta advances valuation: both expiry and ex-dividend times move.
        var earlier = ShiftTime(request, -dt);
        var later = ShiftTime(request, dt);
        var theta = request.Dividends == DividendKind.DiscreteCash
            ? (CashDividendModel.Evaluate(earlier, volatility, settings, cancellationToken, request).Price -
                CashDividendModel.Evaluate(later, volatility, settings, cancellationToken, request).Price) / (2 * dt)
            : (Evaluate(earlier, volatility, cancellationToken).Price -
                Evaluate(later, volatility, cancellationToken).Price) / (2 * dt);
        return Complete(request, new(core.Price, volatility, core.Delta, core.Gamma, vega, theta, rho));
    }

    /// <summary>Compatibility API: use the shared IV-only solve, then explicitly calculate full Greeks.</summary>
    public PricingResult ImpliedVolatility(in OptionPricingRequest request, double marketPrice,
        CancellationToken cancellationToken = default)
    {
        var solved = SolveImpliedVolatility(request, marketPrice, cancellationToken);
        return solved.Success
            ? Price(request, solved.Value!.Value.Volatility, cancellationToken)
            : Fail(request, solved.Failure);
    }

    /// <summary>Model-consistent inversion with repricing evidence, without a full-Greek postpass.</summary>
    public ImpliedVolatilityResult SolveImpliedVolatility(in OptionPricingRequest request, double marketPrice,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invalid = Validate(request);
        if (invalid != PricingFailure.None) return FailIv(request, invalid);
        if (!double.IsFinite(marketPrice) || marketPrice < 0) return FailIv(request, PricingFailure.InvalidInput);
        if (request.TimeToExpiry == 0) return FailIv(request, PricingFailure.ImpliedVolatilityNotIdentifiable);
        var discount = Math.Exp(-(request.Premium == PremiumKind.FuturesStyle ? 0 : request.Rate) * request.TimeToExpiry);
        var forward = request.Underlying == UnderlyingKind.Futures ? request.UnderlyingPrice :
            request.UnderlyingPrice * Math.Exp((request.Rate - request.DividendYield) * request.TimeToExpiry);
        var lower = discount * Math.Max(Sign(request) * (forward - request.Strike), 0);
        if (request.Dividends == DividendKind.DiscreteCash)
            lower = CashDividendModel.Deterministic(request);
        if (request.Exercise == ExerciseKind.American)
            lower = Math.Max(lower, Math.Max(Sign(request) * (request.UnderlyingPrice - request.Strike), 0));
        var upper = request.Side == OptionSide.Call
            ? request.UnderlyingPrice * Math.Max(1, request.Underlying == UnderlyingKind.Equity ?
                Math.Exp(-request.DividendYield * request.TimeToExpiry) : discount)
            : request.Strike * Math.Max(1, discount);
        var tolerance = settings.PriceTolerance;
        if (marketPrice < lower - tolerance || marketPrice > upper + tolerance)
            return FailIv(request, PricingFailure.PriceOutOfBounds);
        if (marketPrice <= lower + tolerance)
            return FailIv(request, PricingFailure.ImpliedVolatilityNotIdentifiable);
        // CRR probability validity imposes a minimum sigma at finite step count.
        var carry = request.Underlying == UnderlyingKind.Equity ? request.Rate - request.DividendYield : 0;
        double lo = request.Exercise == ExerciseKind.American && request.Dividends != DividendKind.DiscreteCash ?
            Math.Max(1e-7, Math.Abs(carry) * Math.Sqrt(request.TimeToExpiry / settings.Steps) * 1.000001) : 1e-7;
        double ceiling = request.Dividends == DividendKind.DiscreteCash
            ? Math.Min(settings.MaximumVolatility, .5 / Math.Sqrt(request.TimeToExpiry))
            : settings.MaximumVolatility;
        double hi = Math.Min(ceiling, Math.Max(.25, lo * 2));
        var lp = Evaluate(request, lo, cancellationToken).Price;
        var hp = Evaluate(request, hi, cancellationToken).Price;
        while (double.IsFinite(hp) && hp < marketPrice - tolerance && hi < ceiling)
        {
            hi = Math.Min(ceiling, hi * 2);
            hp = Evaluate(request, hi, cancellationToken).Price;
        }
        if (!double.IsFinite(lp) || !double.IsFinite(hp)) return FailIv(request, PricingFailure.NumericalFailure);
        if (marketPrice < lp - tolerance || marketPrice > hp + tolerance)
            return FailIv(request, PricingFailure.VolatilityNotBracketed);
        for (var i = 0; i < settings.MaximumIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mid = (lo + hi) / 2;
            var p = Evaluate(request, mid, cancellationToken).Price;
            if (!double.IsFinite(p)) return FailIv(request, PricingFailure.NumericalFailure);
            if (Math.Abs(p - marketPrice) <= tolerance)
                return new(new(mid, p, p - marketPrice, i + 1), PricingFailure.None, Engine(request), settings.Steps)
                    { Request = request, NumericalPolicy = settings };
            if (p < marketPrice) lo = mid; else hi = mid;
        }
        return FailIv(request, PricingFailure.NonConvergence);
    }

    public void PriceBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> volatilities,
        Span<PricingResult> results, CancellationToken cancellationToken = default)
    {
        if (requests.Length != volatilities.Length || results.Length != requests.Length || requests.Length > 2048)
            throw new ArgumentException("Matching spans with at most 2048 requests are required.");
        for (int i = 0; i < requests.Length; i++) results[i] = Price(requests[i], volatilities[i], cancellationToken);
    }

    public void ImpliedVolatilityBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> prices,
        Span<PricingResult> results, CancellationToken cancellationToken = default)
    {
        if (requests.Length != prices.Length || results.Length != requests.Length || requests.Length > 2048)
            throw new ArgumentException("Matching spans with at most 2048 requests are required.");
        for (int i = 0; i < requests.Length; i++) results[i] = ImpliedVolatility(requests[i], prices[i], cancellationToken);
    }

    public void PriceAndDeltaBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> volatilities,
        Span<PriceDeltaResult> results, CancellationToken cancellationToken = default)
    {
        if (requests.Length != volatilities.Length || results.Length != requests.Length || requests.Length > 2048)
            throw new ArgumentException("Matching spans with at most 2048 requests are required.");
        cancellationToken.ThrowIfCancellationRequested();
        for (int i = 0; i < requests.Length; i++)
            results[i] = PriceAndDelta(requests[i], volatilities[i], cancellationToken);
    }

    public void SolveImpliedVolatilityBatch(ReadOnlySpan<OptionPricingRequest> requests, ReadOnlySpan<double> prices,
        Span<ImpliedVolatilityResult> results, CancellationToken cancellationToken = default)
    {
        if (requests.Length != prices.Length || results.Length != requests.Length || requests.Length > 2048)
            throw new ArgumentException("Matching spans with at most 2048 requests are required.");
        cancellationToken.ThrowIfCancellationRequested();
        for (int i = 0; i < requests.Length; i++)
            results[i] = SolveImpliedVolatility(requests[i], prices[i], cancellationToken);
    }

    private PriceDeltaResult FailDelta(in OptionPricingRequest r, PricingFailure failure) =>
        new(null, failure, Engine(r), settings.Steps) { Request = r, NumericalPolicy = settings };
    private ImpliedVolatilityResult FailIv(in OptionPricingRequest r, PricingFailure failure) =>
        new(null, failure, Engine(r), settings.Steps) { Request = r, NumericalPolicy = settings };

    private PricingFailure Validate(in OptionPricingRequest r)
    {
        if (!double.IsFinite(r.UnderlyingPrice) || r.UnderlyingPrice <= 0 ||
            !double.IsFinite(r.Strike) || r.Strike <= 0 || !double.IsFinite(r.TimeToExpiry) ||
            !double.IsFinite(r.Rate) || !double.IsFinite(r.DividendYield))
            return PricingFailure.InvalidInput;
        if (r.TimeToExpiry < 0) return PricingFailure.Expired;
        if (r.TimeToExpiry > 100 || Math.Abs(r.Rate * r.TimeToExpiry) > 100 ||
            Math.Abs(r.DividendYield * r.TimeToExpiry) > 100) return PricingFailure.InvalidInput;
        if (r.Underlying is not (UnderlyingKind.Futures or UnderlyingKind.Equity) ||
            r.Exercise is not (ExerciseKind.European or ExerciseKind.American) ||
            r.Side is not (OptionSide.Call or OptionSide.Put) ||
            r.Premium is not (PremiumKind.PaidUpfront or PremiumKind.FuturesStyle) ||
            r.Dividends is not (DividendKind.None or DividendKind.ContinuousYield or DividendKind.DiscreteCash))
            return PricingFailure.UnsupportedConvention;
        if (r.Dividends == DividendKind.None && r.DividendYield != 0 ||
            r.Underlying == UnderlyingKind.Futures && (r.Dividends != DividendKind.None || r.DividendYield != 0) ||
            r.Underlying == UnderlyingKind.Equity && r.Premium != PremiumKind.PaidUpfront)
            return PricingFailure.UnsupportedConvention;
        if (r.Dividends != DividendKind.DiscreteCash && !r.CashDividends.IsDefaultOrEmpty)
            return PricingFailure.InvalidInput;
        if (r.Dividends == DividendKind.DiscreteCash)
        {
            if (r.DividendYield != 0 || r.CashDividends.IsDefault || r.CashDividends.Length > 64)
                return PricingFailure.InvalidInput;
            double prior = 0, total = 0;
            foreach (var d in r.CashDividends)
            {
                if (!double.IsFinite(d.Time) || !double.IsFinite(d.Amount) || d.Time <= prior ||
                    d.Time >= r.TimeToExpiry || d.Amount <= 0) return PricingFailure.InvalidInput;
                prior = d.Time;
                total += d.Amount;
            }
            // A bounded grid requires spot to remain an interior point.
            if (!double.IsFinite(total) || total > 4 * Math.Max(r.UnderlyingPrice, r.Strike) ||
                r.UnderlyingPrice / Math.Max(r.UnderlyingPrice, r.Strike) < .05)
                return PricingFailure.UnsupportedConvention;
        }
        return PricingFailure.None;
    }

    private static OptionPricingRequest ShiftTime(in OptionPricingRequest r, double shift)
    {
        if (r.Dividends != DividendKind.DiscreteCash || r.CashDividends.IsEmpty)
            return r with { TimeToExpiry = r.TimeToExpiry + shift };
        var dividends = ImmutableArray.CreateBuilder<CashDividend>(r.CashDividends.Length);
        foreach (var d in r.CashDividends) dividends.Add(d with { Time = d.Time + shift });
        return r with { TimeToExpiry = r.TimeToExpiry + shift, CashDividends = dividends.MoveToImmutable() };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private (double Price, double Delta, double Gamma) Evaluate(in OptionPricingRequest r, double sigma, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (r.Dividends == DividendKind.DiscreteCash)
            return CashDividendModel.Evaluate(r, sigma, settings, token);
        var rate = r.Premium == PremiumKind.FuturesStyle ? 0 : r.Rate;
        var carry = r.Underlying == UnderlyingKind.Equity ? r.Rate - r.DividendYield : 0;
        if (r.Exercise == ExerciseKind.European)
        {
            var forward = r.UnderlyingPrice * Math.Exp(carry * r.TimeToExpiry);
            return (r.Underlying == UnderlyingKind.Equity
                ? OptionModel.PriceManaged(forward, r.Strike, rate, sigma, r.TimeToExpiry, Sign(r))
                : OptionModel.Price(forward, r.Strike, rate, sigma, r.TimeToExpiry, Sign(r)), 0, 0);
        }
        int n = settings.Steps;
        // Prevent terminal nodes from silently underflowing to zero or overflowing.
        if (Math.Abs(Math.Log(r.UnderlyingPrice)) + sigma * Math.Sqrt(r.TimeToExpiry * n) > 650)
            return (double.NaN, 0, 0);
        double dt = r.TimeToExpiry / n, u = Math.Exp(sigma * Math.Sqrt(dt)), d = 1 / u;
        double prob = (Math.Exp(carry * dt) - d) / (u - d), df = Math.Exp(-rate * dt);
        if (!double.IsFinite(prob) || prob < 0 || prob > 1) return (double.NaN, 0, 0);
        var buffer = ArrayPool<double>.Shared.Rent(n + 1);
        try
        {
            double s = r.UnderlyingPrice * Math.Pow(d, n), ratio = u * u;
            for (int j = 0; j <= n; j++, s *= ratio) buffer[j] = Math.Max(Sign(r) * (s - r.Strike), 0);
            double delta = 0, gamma = 0;
            for (int i = n - 1; i >= 0; i--)
            {
                if ((i & 31) == 0) token.ThrowIfCancellationRequested();
                s = r.UnderlyingPrice * Math.Pow(d, i);
                for (int j = 0; j <= i; j++, s *= ratio)
                    buffer[j] = Math.Max(df * ((1 - prob) * buffer[j] + prob * buffer[j + 1]),
                        Math.Max(Sign(r) * (s - r.Strike), 0));
                if (i == 2)
                {
                    var upDelta = (buffer[2] - buffer[1]) / (r.UnderlyingPrice * (u * u - 1));
                    var downDelta = (buffer[1] - buffer[0]) / (r.UnderlyingPrice * (1 - d * d));
                    gamma = (upDelta - downDelta) / (r.UnderlyingPrice * (u * u - d * d) / 2);
                }
                if (i == 1) delta = (buffer[1] - buffer[0]) / (r.UnderlyingPrice * (u - d));
            }
            return (buffer[0], delta, gamma);
        }
        finally { ArrayPool<double>.Shared.Return(buffer); }
    }

    private static int Sign(in OptionPricingRequest r) => r.Side == OptionSide.Call ? 1 : -1;
    private static string Engine(in OptionPricingRequest r) => r.Dividends == DividendKind.DiscreteCash
        ? r.Exercise switch
        {
            ExerciseKind.European => "Equity.European.CashDividend.ImplicitFD/v1",
            ExerciseKind.American => "Equity.American.CashDividend.ImplicitFD/v1",
            _ => "Unsupported"
        }
        : (r.Underlying, r.Exercise) switch
    {
        (UnderlyingKind.Futures, ExerciseKind.European) =>
            OptionPricerBackend.UseRust ? "Black76.Rust/v1" : "Black76.Managed/v1",
        (UnderlyingKind.Equity, ExerciseKind.European) => EuropeanEquityEngine,
        (UnderlyingKind.Futures, ExerciseKind.American) => "AmericanFutures.CRR.Managed/v1",
        (UnderlyingKind.Equity, ExerciseKind.American) => "AmericanEquity.CRR.Managed/v1",
        _ => "Unsupported"
    };
    private PricingResult Fail(in OptionPricingRequest r, PricingFailure failure) =>
        new(null, failure, Engine(r), settings.Steps) { Request = r, NumericalPolicy = settings };
    private PricingResult Complete(in OptionPricingRequest r, OptionValues v) =>
        double.IsFinite(v.Price) && double.IsFinite(v.Delta) && double.IsFinite(v.Gamma) &&
        double.IsFinite(v.Vega) && double.IsFinite(v.Theta) && double.IsFinite(v.Rho)
        ? new(v, PricingFailure.None, Engine(r), settings.Steps) { Request = r, NumericalPolicy = settings }
        : Fail(r, PricingFailure.NumericalFailure);
}
