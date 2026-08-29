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
    public async Task Session_adapter_honors_authoritative_holiday_and_early_close()
    {
        var holiday = new DateOnly(2026, 9, 7);
        var earlyClose = new DateOnly(2026, 11, 27);
        var adapter = new MarketConditionSessionAdapter(new CmeFuturesMarketSessionCalendar(
            [holiday], new Dictionary<DateOnly, TimeOnly> { [earlyClose] = new(13, 0) }));
        var configuration = new MarketConditionSessionConfiguration();

        var holidayResult = await adapter.ReadOnceAsync("ES",
            configuration, new DateTime(2026, 9, 7, 15, 0, 0, DateTimeKind.Utc), default);
        var afterEarlyClose = await adapter.ReadOnceAsync("ES",
            configuration, new DateTime(2026, 11, 27, 19, 0, 0, DateTimeKind.Utc), default);

        holidayResult.Status.Should().Be(MarketSessionStatus.Closed);
        holidayResult.IsEntryWindow.Should().BeFalse();
        afterEarlyClose.Status.Should().Be(MarketSessionStatus.Closed);
        afterEarlyClose.IsEntryWindow.Should().BeFalse();
    }

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

        var result = await new MarketConditionEventRiskAdapter(factory).ReadOnceAsync(
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
        var adapter = new MarketConditionEventRiskAdapter(factory);
        var action = () => adapter.ReadOnceAsync(new MarketConditionEventRiskConfiguration
        {
            RequiredEventCategories = ["UnmappedCategory"]
        }, DateTime.UtcNow, default).AsTask();

        (await action.Should().ThrowAsync<MarketConditionCalculationException>())
            .Which.Category.Should().Be(MarketConditionFailureCategory.ConfigurationUnavailable);
    }

    [Fact]
    public void Missing_ibkr_authority_is_explicitly_fail_closed()
    {
        var at = new DateTime(2026, 8, 28, 18, 0, 0, DateTimeKind.Utc);
        var result = new UnavailableMarketConditionBrokerReadiness().Read(at);

        result.Status.Should().Be(MarketOperationalStatus.Unavailable);
        result.ObservedAtUtc.Should().Be(at);
        result.SequenceId.Should().Be(at.Ticks);
    }

    [Fact]
    public async Task Provider_uses_live_coordinator_once_when_no_test_snapshot_is_seeded()
    {
        var at = new DateTime(2026, 8, 28, 18, 0, 0, DateTimeKind.Utc);
        var command = MarketConditionFunctionExecutionTests.Command(at);
        var coordinator = new RecordingCoordinator(ValidSnapshot(command, at));
        var provider = new MarketConditionSnapshotProvider(coordinator);
        provider.Clear();

        var result = await provider.CaptureAtAsync(command, at);

        result.Outcome.Should().Be(MarketConditionCaptureOutcome.Success);
        coordinator.Calls.Should().Be(1);
        result.Snapshot.SnapshotSha256.Should().Be(MarketConditionSnapshotHash.Compute(result.Snapshot));
    }

    static MarketConditionSnapshot ValidSnapshot(
        ExecuteMarketConditionPipelineCommand command,
        DateTime at)
    {
        MarketSourceObservation O(string id) => new()
        {
            SourceId = id, SourceTimestampUtc = at.AddSeconds(-1), ReceivedAtUtc = at,
            SequenceId = 1, Availability = MarketSourceAvailability.Available,
            Validity = MarketSourceValidity.Valid
        };
        return new MarketConditionSnapshot
        {
            WorkflowId = command.WorkflowId, EntityId = command.WorkflowEntityId, FundId = command.FundId,
            InstrumentRoot = "ES", TargetHorizon = command.TargetHorizon, EvaluationTimestampUtc = at,
            FuturesQuote = new MarketConditionFuturesQuote
            {
                QuoteObservation = O("PrimaryFuturesFeed"), TradeObservation = O("PrimaryFuturesTrade")
            },
            OptionChainQuality = new MarketConditionOptionChainQuality { Observation = O("OptionChain") },
            SessionState = new MarketConditionSessionState { Observation = O("SessionCalendar") },
            EventRiskState = new MarketConditionEventRiskState { Observation = O("EventRiskCalendar") },
            VolatilityShockState = new MarketConditionVolatilityShockState { Observation = O("VolatilityVX") },
            OperationalHealth = [], DataQualityItems = []
        };
    }

    sealed class RecordingCoordinator(MarketConditionSnapshot snapshot) : IMarketConditionSnapshotAdapterCoordinator
    {
        public int Calls { get; private set; }
        public ValueTask<MarketConditionSnapshot> PublishAsync(
            ExecuteMarketConditionPipelineCommand command,
            MarketConditionWorkflowEligibilityState workflowEligibility, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(snapshot);
        }
    }
}
