using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class NativeAbiTests
{
    [Fact]
    public void ManagedRecordsMatchNativeAbiSizes()
    {
        Assert.Equal(32, Marshal.SizeOf<MarketRecordHeader32>());
        Assert.Equal(64, Marshal.SizeOf<QuoteRecord64>());
        Assert.Equal(64, Marshal.SizeOf<TradeRecord64>());
        Assert.Equal(64, Marshal.SizeOf<MboRecord64>());
        Assert.Equal(64, Marshal.SizeOf<StatisticsRecord64>());
        Assert.Equal(64, Marshal.SizeOf<MarketRecord64>());
        Assert.Equal(128, Marshal.SizeOf<NativeFeedConfig>());
        Assert.Equal(32, Marshal.SizeOf<NativeTickerSubscription>());
        Assert.Equal(32, Marshal.SizeOf<NativeTickerInstrumentMapping>());
        Assert.Equal(32, Marshal.SizeOf<NativeOptionChainSubscription>());
        Assert.Equal(32, Marshal.SizeOf<NativeOptionContractSelection>());
        Assert.Equal(32, Marshal.SizeOf<NativeWaitResult>());
        Assert.Equal(32, Marshal.SizeOf<NativeBatchResult>());
        Assert.Equal(128, Marshal.SizeOf<NativeFeedStats>());
        Assert.Equal(8, Marshal.SizeOf<NativeUtf8Slice>());
        Assert.Equal(64, Marshal.SizeOf<NativeContractQuery>());
        Assert.Equal(192, Marshal.SizeOf<NativeContractDetail>());
        Assert.Equal(88, Marshal.SizeOf<NativeLatestPriceRequest>());
        Assert.Equal(64, Marshal.SizeOf<LatestPriceResult64>());
    }

    [Fact]
    public void LoadedNativeLibraryHasExpectedAbiVersion()
    {
        Assert.Equal(NativeConstants.AbiVersion, NativeMethods.GetAbiVersion());
    }
}
