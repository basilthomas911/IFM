using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
public sealed class DailyCalendarCacheTests
{
    [Fact]
    public async Task Full_day_is_loaded_once_but_FOMC_blackouts_are_recomputed_and_refresh_invalidates()
    {
        var at=new DateTime(2026,11,19,12,0,0,DateTimeKind.Utc);
        var factory=Substitute.For<IDbContextFactory>();var db=Substitute.For<IMarketDataDbContext>();factory.MarketDataDb.Returns(db);
        var logs=Substitute.For<IDownloadLogQueryApi>();var failed=false;
        logs.GetHistoryAsync(Arg.Any<MarketDataDownloadPartition>(),1,null,Arg.Any<CancellationToken>()).Returns(call=>
        {
            var partition=call.Arg<MarketDataDownloadPartition>();
            var o=new MarketDataDownloadOutcome{Dataset=MarketDataDownloadDataset.EconomicCalendar,Scope=partition.Scope,
                ValueDate=partition.ValueDate,ImportCommandId=Guid.NewGuid(),SourceTerminalEventId=Guid.NewGuid(),
                RequestedAtUtc=at.AddHours(-1),StartedAtUtc=at.AddHours(-1),FinishedAtUtc=at.AddHours(-1),
                Status=failed?MarketDataDownloadStatus.Failed:MarketDataDownloadStatus.Completed,DownloadedRecordCount=1,PersistedRecordCount=1,
                ErrorCode=failed?"TEST":null,ErrorMessage=failed?"refresh failed":null};
            return new ServiceOk<MarketDataDownloadHistoryResult>(new([new(o,MarketDataDownloadOutcome.LoggingCommandId(o.ImportCommandId),o.ComputeHash(),o.FinishedAtUtc)],null));
        });
        db.GetEconomicCalendarsAsync(Arg.Any<EconomicCalendarDateRange>(),Arg.Any<CancellationToken>()).Returns(new List<EconomicCalendarReadModel>
            {new(){CountryCode="US",EventName="FOMC rate decision",EventDate=at.AddHours(2),Impact="High"}});
        var adapter=new MarketConditionEventRiskAdapter(factory,logs,preparedOnly:true);var policy=new MarketConditionEventRiskConfiguration();
        Assert.Equal(MarketEventRiskStatus.Unknown,(await adapter.ReadOnceAsync(policy,at,default)).Status);
        Assert.Empty(logs.ReceivedCalls());Assert.Empty(db.ReceivedCalls());
        await adapter.WarmAsync(policy,at,default);
        Assert.Equal(MarketEventRiskStatus.Clear,(await adapter.ReadOnceAsync(policy,at,default)).Status);
        var reads=logs.ReceivedCalls().Count();
        Assert.Equal(MarketEventRiskStatus.Blocked,(await adapter.ReadOnceAsync(policy,at.AddMinutes(90),default)).Status);
        Assert.Equal(MarketEventRiskStatus.Clear,(await adapter.ReadOnceAsync(policy,at.AddMinutes(141),default)).Status);
        Assert.Equal(reads,logs.ReceivedCalls().Count());Assert.Single(db.ReceivedCalls());
        failed=true;ReadCacheInvalidation.CalendarChanged();
        Assert.Equal(MarketEventRiskStatus.Unknown,(await adapter.ReadOnceAsync(policy,at.AddMinutes(142),default)).Status);
        Assert.Equal(reads,logs.ReceivedCalls().Count());
        await adapter.WarmAsync(policy,at.AddMinutes(142),default);
        Assert.Equal(MarketEventRiskStatus.Unknown,(await adapter.ReadOnceAsync(policy,at.AddMinutes(142),default)).Status);
        Assert.True(logs.ReceivedCalls().Count()>reads);
    }
}
