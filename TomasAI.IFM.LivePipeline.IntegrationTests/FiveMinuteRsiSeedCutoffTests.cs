using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class FiveMinuteRsiSeedCutoffTests
{
    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    public void Late_calculation_time_does_not_shift_completed_seed_windows(int period)
    {
        var end=new DateTimeOffset(2026,11,19,20,0,0,TimeSpan.Zero);var now=end.AddMinutes(6);
        var calendar=new CmeFuturesMarketSessionCalendar();
        var windows=RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes,48,end,calendar);
        var series=MarketSeriesIdentity.ForContract("ES20261218");
        var bars=windows.Select((w,i)=>new FuturesTradeSessionBarReadModel {MarketSeriesIdentity=series,ContractId="ES20261218",TimeFrame=TimeFrameType.FiveMinutes,
            ValueDate=w.ValueDate,IntervalStartUtc=w.StartUtc,IntervalEndUtc=w.EndUtc,ObservationId=FuturesTradeSessionBarId.Create(series,TimeFrameType.FiveMinutes,w.EndUtc,i+1),
            Open=100+i,High=101+i,Low=99+i,Close=100+i,Volume=1,TradeCount=1,PriceVolumeSum=100+i,FirstSourceSequence=i+1,LastSourceSequence=i+1,
            FirstMarketEventUtc=w.StartUtc.AddSeconds(1),LastMarketEventUtc=w.StartUtc.AddSeconds(1),CalculatedAtUtc=now,
            CalculationMethod=MarketSignalCalculationMethod.NormalizedHistoricalAggregate,SchemaVersion=1,CalculationVersion="stored-trades-rebuilt-v1",IsComplete=true,IsValid=true}).ToArray();
        var command=ApiApplicationStartupActivities.CreateSeededFiveMinuteRsiCommand(new("ES20261218",new(2026,11,19),TimeFrameType.FiveMinutes,period),bars);
        Assert.Equal(end,command.HistoricalSeed!.AsOfUtc);
        Assert.Equal("RSI.SEED_APPLIED",FuturesRsiHistoricalSeedModel.Decide(command,null,calendar,now).Reason);
        var incorrect=command with{HistoricalSeed=command.HistoricalSeed with{AsOfUtc=now}};
        Assert.Equal("RSI.SEED_NONCONTIGUOUS_EMPTY",FuturesRsiHistoricalSeedModel.Decide(incorrect,null,calendar,now).Reason);
        Assert.All(bars,b=>Assert.Equal(now,b.CalculatedAtUtc));
    }
}

