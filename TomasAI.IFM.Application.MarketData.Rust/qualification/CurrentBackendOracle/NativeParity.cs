using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Shared.MarketData.Contracts;

/// <summary>Invokes the new DLL directly; no legacy resolver or runtime is modified.</summary>
internal static class NativeParity
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint Version();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TreasuryRate(double halfYield, ref double output);

    /// <summary>Compares actual cross-ABI floating-point bits against unchanged C#.</summary>
    internal static void Check(string library, decimal[] rates, TreasuryRateConversionPolicy policy,
        DateOnly date, DateTimeOffset retrieved)
    {
        var handle = NativeLibrary.Load(Path.GetFullPath(library));
        try
        {
            var version = Marshal.GetDelegateForFunctionPointer<Version>(NativeLibrary.GetExport(handle, "ifm_market_data_get_abi_version"));
            if (version() != 1) throw new InvalidDataException("Unexpected MarketData ABI identity.");
            var convert = Marshal.GetDelegateForFunctionPointer<TreasuryRate>(NativeLibrary.GetExport(handle, "ifm_market_data_treasury_rate_v1"));
            foreach (var rate in rates)
            {
                var expected = TreasuryRateConversion.Convert(new(date, [new(TreasuryTenor.OneMonth, rate)], retrieved, UsTreasuryCurve.Source), TreasuryTenor.OneMonth, policy);
                var actual = 123d;
                var status = convert((double)(rate / 200m), ref actual);
                if (expected.Succeeded)
                {
                    if (status != 0 || BitConverter.DoubleToUInt64Bits(actual) != BitConverter.DoubleToUInt64Bits(expected.Value!.AnnualContinuousRate))
                        throw new InvalidDataException($"Native Treasury difference for CMT {rate}: status={status}, C#={expected.Value!.AnnualContinuousRate:R}, Rust={actual:R}.");
                }
                else if (status != 2 || actual != 123d)
                    throw new InvalidDataException("Native domain failure must preserve the caller output.");
            }
            Console.WriteLine($"Native ABI version 1; {rates.Length} Treasury results match C# exactly.");
        }
        finally
        {
            // These qualification calls are synchronous; no runtime/task is
            // created by them. Never unload a library running native work.
            NativeLibrary.Free(handle);
        }
    }
}
