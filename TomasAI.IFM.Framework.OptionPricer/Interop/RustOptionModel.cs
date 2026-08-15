using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Framework.OptionPricer.Interop;

internal static class RustOptionModel
{
    static RustOptionModel()
    {
        var actualVersion = NativeMethods.GetAbiVersion();
        if (actualVersion != OptionPricerNativeConstants.AbiVersion)
        {
            throw new InvalidOperationException(
                $"Rust option-pricer ABI version {actualVersion} does not match required version " +
                $"{OptionPricerNativeConstants.AbiVersion}.");
        }
    }

    internal static void EnsureAvailable()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Price(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double volatility,
        double timeToExpiry,
        int optionType)
    {
        var status = NativeMethods.Price(
            forwardPrice,
            strikePrice,
            riskFreeRate,
            volatility,
            timeToExpiry,
            optionType,
            out var result);
        if (status != OptionPricerNativeStatus.Ok)
            ThrowInfrastructureFailure(status, nameof(Price));
        return result;
    }

    internal static Black76Result PriceWithGreeks(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double volatility,
        double timeToExpiry,
        int optionType)
    {
        var status = NativeMethods.PriceWithGreeks(
            forwardPrice,
            strikePrice,
            riskFreeRate,
            volatility,
            timeToExpiry,
            optionType,
            out var result);
        if (status == OptionPricerNativeStatus.InvalidArgument)
            ThrowPositiveRequired();
        if (status != OptionPricerNativeStatus.Ok)
            ThrowInfrastructureFailure(status, nameof(PriceWithGreeks));
        return result;
    }

    internal static double ImpliedVolatility(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double marketPrice,
        double timeToExpiry,
        int optionType,
        double tolerance,
        int maxIterations,
        double? initialGuess)
    {
        var status = NativeMethods.ImpliedVolatility(
            forwardPrice,
            strikePrice,
            riskFreeRate,
            marketPrice,
            timeToExpiry,
            optionType,
            tolerance,
            maxIterations,
            initialGuess.HasValue ? 1 : 0,
            initialGuess.GetValueOrDefault(),
            out var result);
        if (status == OptionPricerNativeStatus.NoConvergence)
            return double.NaN;
        if (status == OptionPricerNativeStatus.InvalidArgument)
            ThrowInvalidSolverArgument();
        if (status != OptionPricerNativeStatus.Ok)
            ThrowInfrastructureFailure(status, nameof(ImpliedVolatility));
        return result;
    }

    internal static bool TryImpliedVolatilityWithGreeks(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double marketPrice,
        double timeToExpiry,
        int optionType,
        double tolerance,
        int maxIterations,
        double? initialGuess,
        out double impliedVolatility,
        out Black76Result result)
    {
        var status = NativeMethods.ImpliedVolatilityWithGreeks(
            forwardPrice,
            strikePrice,
            riskFreeRate,
            marketPrice,
            timeToExpiry,
            optionType,
            tolerance,
            maxIterations,
            initialGuess.HasValue ? 1 : 0,
            initialGuess.GetValueOrDefault(),
            out var nativeResult);
        if (status is OptionPricerNativeStatus.NoConvergence or OptionPricerNativeStatus.InvalidArgument)
        {
            impliedVolatility = double.NaN;
            result = default;
            return false;
        }
        if (status != OptionPricerNativeStatus.Ok)
            ThrowInfrastructureFailure(status, nameof(TryImpliedVolatilityWithGreeks));

        impliedVolatility = nativeResult.ImpliedVolatility;
        result = new Black76Result(
            nativeResult.Price,
            nativeResult.Delta,
            nativeResult.Gamma,
            nativeResult.Vega,
            nativeResult.Theta,
            nativeResult.Rho);
        return true;
    }

    internal static unsafe void PriceBatch(
        ReadOnlySpan<double> forwardPrices,
        ReadOnlySpan<double> strikePrices,
        ReadOnlySpan<double> riskFreeRates,
        ReadOnlySpan<double> volatilities,
        ReadOnlySpan<double> timesToExpiry,
        ReadOnlySpan<int> optionTypes,
        Span<double> results)
    {
        if (results.IsEmpty)
            return;

        fixed (double* forwardPointer = forwardPrices)
        fixed (double* strikePointer = strikePrices)
        fixed (double* ratePointer = riskFreeRates)
        fixed (double* volatilityPointer = volatilities)
        fixed (double* expiryPointer = timesToExpiry)
        fixed (int* optionTypePointer = optionTypes)
        fixed (double* resultPointer = results)
        {
            var status = NativeMethods.PriceBatch(
                forwardPointer,
                strikePointer,
                ratePointer,
                volatilityPointer,
                expiryPointer,
                optionTypePointer,
                (uint)results.Length,
                resultPointer,
                out _);
            if (status != OptionPricerNativeStatus.Ok)
                ThrowInfrastructureFailure(status, nameof(PriceBatch));
        }
    }

    internal static unsafe void PriceWithGreeksBatch(
        ReadOnlySpan<double> forwardPrices,
        ReadOnlySpan<double> strikePrices,
        ReadOnlySpan<double> riskFreeRates,
        ReadOnlySpan<double> volatilities,
        ReadOnlySpan<double> timesToExpiry,
        ReadOnlySpan<int> optionTypes,
        Span<Black76Result> results)
    {
        if (results.IsEmpty)
            return;

        fixed (double* forwardPointer = forwardPrices)
        fixed (double* strikePointer = strikePrices)
        fixed (double* ratePointer = riskFreeRates)
        fixed (double* volatilityPointer = volatilities)
        fixed (double* expiryPointer = timesToExpiry)
        fixed (int* optionTypePointer = optionTypes)
        fixed (Black76Result* resultPointer = results)
        {
            var status = NativeMethods.PriceWithGreeksBatch(
                forwardPointer,
                strikePointer,
                ratePointer,
                volatilityPointer,
                expiryPointer,
                optionTypePointer,
                (uint)results.Length,
                resultPointer,
                out _);
            if (status == OptionPricerNativeStatus.InvalidArgument)
                ThrowPositiveRequired();
            if (status != OptionPricerNativeStatus.Ok)
                ThrowInfrastructureFailure(status, nameof(PriceWithGreeksBatch));
        }
    }

    [DoesNotReturn]
    private static void ThrowPositiveRequired() =>
        throw new ArgumentOutOfRangeException("F and K must be positive.");

    [DoesNotReturn]
    private static void ThrowInvalidSolverArgument() =>
        throw new ArgumentOutOfRangeException("A required implied-volatility argument is invalid.");

    [DoesNotReturn]
    private static void ThrowInfrastructureFailure(OptionPricerNativeStatus status, string operation) =>
        throw new InvalidOperationException(
            $"Rust option-pricer operation '{operation}' failed with native status {status} ({(int)status}).");
}
