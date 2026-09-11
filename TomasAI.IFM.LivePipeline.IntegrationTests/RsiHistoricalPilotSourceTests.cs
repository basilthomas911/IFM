using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.ParameterSets;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class RsiHistoricalPilotSourceTests
{
 [Theory]
 [InlineData(TimeFrameType.FifteenSeconds)][InlineData(TimeFrameType.OneMinute)][InlineData(TimeFrameType.FiveMinutes)]
 [InlineData(TimeFrameType.FifteenMinutes)][InlineData(TimeFrameType.OneHour)][InlineData(TimeFrameType.FourHours)][InlineData(TimeFrameType.Daily)]
 public async Task Complete_retained_prices_produce_exact_completed_windows(TimeFrameType interval)
 {
  var storage=Substitute.For<IDbContextFactory>();var eod=Substitute.For<IHistoricalObservationStore>();var calendar=new CmeFuturesMarketSessionCalendar();
  storage.MarketDataDb.GetFuturesTickAtOrBeforeAsync(Arg.Any<string>(),Arg.Any<DateOnly>(),Arg.Any<TimeOnly>(),Arg.Any<CancellationToken>())
   .Returns(call=>Task.FromResult<FuturesTickDataV2ReadModel?>(new(call.Arg<string>(),call.Arg<DateOnly>(),42,call.Arg<TimeOnly>(),100m,1)));
  eod.GetRawEodAsync(Arg.Any<MarketSeriesIdentity>(),Arg.Any<DateOnly>(),Arg.Any<CancellationToken>()).Returns(call=>
  {
   var session=calendar.GetSession(call.Arg<DateOnly>());
   return ValueTask.FromResult<FuturesEodObservationReadModel?>(new(){ValueDate=session.ValueDate,SessionStartUtc=session.StartUtc,SessionEndUtc=session.EndUtc,
    LastMarketEventUtc=session.EndUtc.AddTicks(-1),Close=100m,IsComplete=true,IsValid=true});
  });
  var source=new RsiHistoricalPilotStartup(Substitute.For<IMarketDataAnalyticsCommandApi>(),Substitute.For<IActorService>(),storage,eod,calendar,TimeProvider.System,NullLogger<RsiHistoricalPilotStartup>.Instance,new(true));
  var cutoff=new DateTimeOffset(2026,8,25,18,0,0,TimeSpan.Zero);var entity=FuturesRsiSignalEntityId.Create("ES-PILOT",new DateOnly(2026,8,25),interval,14);
  var bars=await source.ReadAsync(entity,16,cutoff,default);
  Assert.Equal(16,bars.Length);Assert.All(bars,x=>Assert.Empty(new FuturesTradeSessionBarReadModelValidationRules().Execute(x)));
  Assert.Equal(RsiHistoricalSeedWindowModel.Create(interval,16,cutoff,calendar).Select(x=>x.EndUtc),bars.Select(x=>x.IntervalEndUtc));
  Assert.All(bars,x=>Assert.Equal(0,x.LastSourceSequence));
  if(interval==TimeFrameType.Daily)await storage.MarketDataDb.DidNotReceive().GetFuturesTickAtOrBeforeAsync(Arg.Any<string>(),Arg.Any<DateOnly>(),Arg.Any<TimeOnly>(),Arg.Any<CancellationToken>());
 }
 [Fact]public async Task Missing_one_price_discards_the_whole_seed()
 {
  var storage=Substitute.For<IDbContextFactory>();var count=0;
  storage.MarketDataDb.GetFuturesTickAtOrBeforeAsync(Arg.Any<string>(),Arg.Any<DateOnly>(),Arg.Any<TimeOnly>(),Arg.Any<CancellationToken>())
   .Returns(call=>Task.FromResult<FuturesTickDataV2ReadModel?>(++count==5?null:new(call.Arg<string>(),call.Arg<DateOnly>(),count,call.Arg<TimeOnly>(),100m,1)));
  var source=new RsiHistoricalPilotStartup(Substitute.For<IMarketDataAnalyticsCommandApi>(),Substitute.For<IActorService>(),storage,Substitute.For<IHistoricalObservationStore>(),new CmeFuturesMarketSessionCalendar(),TimeProvider.System,NullLogger<RsiHistoricalPilotStartup>.Instance,new(true));
  Assert.Empty(await source.ReadAsync(FuturesRsiSignalEntityId.Create("ES",new DateOnly(2026,8,25),TimeFrameType.OneHour,14),16,new DateTimeOffset(2026,8,25,18,0,0,TimeSpan.Zero),default));
 }
}
