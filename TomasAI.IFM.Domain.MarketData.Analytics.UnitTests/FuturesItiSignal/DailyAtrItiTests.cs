using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using Xunit;
namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;
public sealed class DailyAtrItiTests
{
    static readonly DateTime At=new(2026,11,19,15,0,0,DateTimeKind.Utc);
    static GenerateFuturesItiSignalCommand Command(double price,double atr=8)=>new("ES20261218",new(2026,11,19),TimeFrameType.Daily,At,price,20)
        {RequireDailyAtr=true,DailyAtr=new("ES20261218",atr,At.AddMinutes(-1),At.AddSeconds(-30),123)};
    [Fact]
    public void ATR_is_frozen_between_DCs_and_Daily_Trending_resumes_at_band_cadence()
    {
        Assert.True(FuturesItiSignalCompute.TryCompute(Command(100),null,out var dc));Assert.Equal(8,dc.Threshold);
        Assert.True(FuturesItiSignalCompute.TryCompute(Command(102,12) with{Timestamp=At.AddSeconds(1)},dc,out var trending));
        Assert.Equal(IntrinsicTimeModeType.Trending,trending.IntrinsicTimeMode);Assert.Equal(8,trending.Threshold);Assert.Equal(dc.DailyAtr,trending.DailyAtr);
        Assert.False(FuturesItiSignalCompute.TryCompute(Command(102.25,12) with{Timestamp=At.AddSeconds(2)},trending,out _));
        Assert.True(FuturesItiSignalCompute.TryCompute(Command(92,12) with{Timestamp=At.AddSeconds(3)},trending,out var down));
        Assert.Equal(IntrinsicTimeModeType.TrendDirectionChanged,down.IntrinsicTimeMode);Assert.Equal(12,down.Threshold);
        var restored=MessagePackSerializer.Deserialize<GenerateFuturesItiSignalCommand>(MessagePackSerializer.Serialize(Command(100)));
        Assert.Equal(8,restored.DailyAtr!.Value);Assert.True(restored.RequireDailyAtr);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)]
    public void Invalid_ATR_cannot_generate_a_new_DC(double atr)
        =>Assert.False(FuturesItiSignalCompute.TryCompute(Command(100,atr),null,out _));
    [Fact]
    public void Missing_stale_and_future_ATR_cannot_generate_a_new_DC()
    {
        var c=Command(100);
        Assert.False(FuturesItiSignalCompute.TryCompute(c with{DailyAtr=null},null,out _));
        Assert.False(FuturesItiSignalCompute.TryCompute(c with{DailyAtr=c.DailyAtr! with{ObservedAtUtc=At.AddMinutes(-11)}},null,out _));
        Assert.False(FuturesItiSignalCompute.TryCompute(c with{DailyAtr=c.DailyAtr! with{CalculatedAtUtc=At.AddSeconds(1)}},null,out _));
    }
}
