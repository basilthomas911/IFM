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
        Assert.Equal(64, Marshal.SizeOf<NativeHistoricalRequest>());
        Assert.Equal(32, Marshal.SizeOf<NativeHistoricalEstimate>());
        Assert.Equal(120, Marshal.SizeOf<NativeHistoricalRecord>());
        Assert.Equal(24, Marshal.SizeOf<NativeHistoricalBatch>());
        Assert.Equal(40, Marshal.OffsetOf<StatisticsRecord64>(
            nameof(StatisticsRecord64.Quantity)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<StatisticsRecord64>(
            nameof(StatisticsRecord64.ReferenceTimestampNanoseconds)).ToInt32());
        Assert.Equal(104, Marshal.OffsetOf<NativeFeedConfig>(
            nameof(NativeFeedConfig.StatisticsReplayStartTimestampNanoseconds)).ToInt32());
        Assert.Equal(112, Marshal.OffsetOf<NativeFeedConfig>(
            nameof(NativeFeedConfig.TradeReplayStartTimestampNanoseconds)).ToInt32());
    }

    [Fact]
    public void LoadedNativeLibraryHasExpectedAbiVersion()
    {
        Assert.Equal(3u, NativeConstants.AbiVersion);
        Assert.Equal(NativeConstants.AbiVersion, NativeMethods.GetAbiVersion());
    }
}
