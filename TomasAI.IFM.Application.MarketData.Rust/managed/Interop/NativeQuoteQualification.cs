using System.Runtime.InteropServices;

namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeDecimal
{
    internal uint Low, Middle, High, Flags;
    internal static NativeDecimal From(decimal value)
    {
        Span<int> bits = stackalloc int[4]; decimal.GetBits(value, bits);
        return new() { Low = (uint)bits[0], Middle = (uint)bits[1], High = (uint)bits[2], Flags = (uint)bits[3] };
    }
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativePricingQuote
{
    internal ulong GenerationLow, GenerationHigh;
    internal long Event, Received, Sequence;
    internal NativeDecimal Bid, Ask, BidSize, AskSize;
    internal int EventOffset, ReceivedOffset;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeQuoteQualificationRequest
{
    internal uint Size, Flags;
    internal ulong GenerationLow, GenerationHigh;
    internal long At;
    internal uint MaximumAge, MaximumSkew, MaximumLead, Reserved;
    internal NativeDecimal Strike, DefinitionStrike;
    internal NativePricingQuote Underlying, Option;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeQuoteQualificationResult { internal uint Size, Failure, Input, Reserved; }
internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int QualifyQuotesCall(in NativeQuoteQualificationRequest request, out NativeQuoteQualificationResult result);
    internal QualifyQuotesCall QualifyQuotes = null!;
    void BindQuoteQualification(nint module)
    {
        if (Marshal.SizeOf<NativePricingQuote>() != 112 || Marshal.SizeOf<NativeQuoteQualificationRequest>() != 304 || Marshal.SizeOf<NativeQuoteQualificationResult>() != 16)
            throw new InvalidDataException("Native quote qualification layout mismatch.");
        QualifyQuotes = Marshal.GetDelegateForFunctionPointer<QualifyQuotesCall>(NativeLibrary.GetExport(module, "ifm_market_data_qualify_quotes_v1"));
    }
}
