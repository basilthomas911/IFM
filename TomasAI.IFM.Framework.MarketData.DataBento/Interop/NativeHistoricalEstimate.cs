using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeHistoricalEstimate
{
    public uint StructSize;
    public uint AbiVersion;
    public double EstimatedCostUsd;
    public ulong EstimatedBytes;
    public ulong EstimatedRecords;
}
