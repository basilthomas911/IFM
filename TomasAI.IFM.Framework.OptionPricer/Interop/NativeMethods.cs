using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace TomasAI.IFM.Framework.OptionPricer.Interop;

internal static partial class NativeMethods
{
    private const string LibraryName = OptionPricerNativeLibraryResolver.LibraryName;

    [LibraryImport(LibraryName, EntryPoint = "ifm_option_pricer_get_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint GetAbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "ifm_black76_price_v1")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial OptionPricerNativeStatus Price(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double volatility,
        double timeToExpiry,
        int optionType,
        out double result);

    [LibraryImport(LibraryName, EntryPoint = "ifm_black76_price_with_greeks_v1")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial OptionPricerNativeStatus PriceWithGreeks(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double volatility,
        double timeToExpiry,
        int optionType,
        out Black76Result result);

    [LibraryImport(LibraryName, EntryPoint = "ifm_black76_implied_volatility_v1")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial OptionPricerNativeStatus ImpliedVolatility(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double marketPrice,
        double timeToExpiry,
        int optionType,
        double tolerance,
        int maxIterations,
        int hasInitialGuess,
        double initialGuess,
        out double result);

    [LibraryImport(LibraryName, EntryPoint = "ifm_black76_implied_volatility_with_greeks_v1")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial OptionPricerNativeStatus ImpliedVolatilityWithGreeks(
        double forwardPrice,
        double strikePrice,
        double riskFreeRate,
        double marketPrice,
        double timeToExpiry,
        int optionType,
        double tolerance,
        int maxIterations,
        int hasInitialGuess,
        double initialGuess,
        out NativeImpliedGreeksResult result);

    [LibraryImport(LibraryName, EntryPoint = "ifm_black76_price_batch_v1")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial OptionPricerNativeStatus PriceBatch(
        double* forwardPrices,
        double* strikePrices,
        double* riskFreeRates,
        double* volatilities,
        double* timesToExpiry,
        int* optionTypes,
        uint count,
        double* results,
        out uint errorIndex);

    [LibraryImport(LibraryName, EntryPoint = "ifm_black76_price_with_greeks_batch_v1")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial OptionPricerNativeStatus PriceWithGreeksBatch(
        double* forwardPrices,
        double* strikePrices,
        double* riskFreeRates,
        double* volatilities,
        double* timesToExpiry,
        int* optionTypes,
        uint count,
        Black76Result* results,
        out uint errorIndex);
}
