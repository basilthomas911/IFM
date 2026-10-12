using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 88)]
internal unsafe struct NativeLatestPriceRequest
{
    public uint StructSize;
    public uint AbiVersion;
    public LatestPricePolicy SelectedPolicy;
    private readonly byte _selectedPolicyPadding0;
    private readonly byte _selectedPolicyPadding1;
    private readonly byte _selectedPolicyPadding2;
    public LatestPriceFreshnessPolicy FreshnessPolicy;
    private readonly byte _freshnessPadding0;
    private readonly byte _freshnessPadding1;
    private readonly byte _freshnessPadding2;
    public DatabentoInputSymbology InputSymbology;
    private readonly byte _symbologyPadding0;
    private readonly byte _symbologyPadding1;
    private readonly byte _symbologyPadding2;
    public uint ReplayLookbackMilliseconds;
    public NativeUtf8Slice Dataset;
    public NativeUtf8Slice Symbol;
    public byte* Utf8Blob;
    public uint Utf8BlobBytes;
    public uint Reserved32;
    public fixed ulong Reserved[4];
}
