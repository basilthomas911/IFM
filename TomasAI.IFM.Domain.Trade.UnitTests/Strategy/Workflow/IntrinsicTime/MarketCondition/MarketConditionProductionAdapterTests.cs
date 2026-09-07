using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionProductionAdapterTests
{
    [Fact]
    public async Task Event_adapter_blocks_rate_decision_in_its_exact_window()
    {
        var at = new DateTime(2026, 8, 28, 18, 0, 0, DateTimeKind.Utc);
        var db = Substitute.For<IMarketDataDbContext>();
        db.GetEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), "US", Arg.Any<CancellationToken>())
            .Returns(new List<EconomicCalendarReadModel>
            {
                new(at.AddMinutes(20), "US", "FOMC Rate Decision", null, null, null,
                    at.AddHours(-1), "import", "High")
            });
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(db);

        var logs = CalendarDownloadFixture.Queries(CalendarDownloadFixture.Row(date: DateOnly.FromDateTime(at), finished: at.AddHours(-1)));
        var result = await new MarketConditionEventRiskAdapter(factory, logs).ReadOnceAsync(
            new MarketConditionEventRiskConfiguration(), at, default);

        result.Status.Should().Be(MarketEventRiskStatus.Blocked);
        result.Category.Should().Be("RateDecision");
        result.EventId.Should().Contain("FOMC Rate Decision");
        await db.Received(1).GetEconomicCalendarsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), "US",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Event_adapter_rejects_unknown_required_category()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var adapter = new MarketConditionEventRiskAdapter(factory, CalendarDownloadFixture.Queries());
        var action = () => adapter.ReadOnceAsync(new MarketConditionEventRiskConfiguration
        {
            RequiredEventCategories = ["UnmappedCategory"]
        }, DateTime.UtcNow, default).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

}
