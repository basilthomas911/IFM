using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataOperationsHealthServiceTests
{
    [Fact]
    public void Empty_snapshot_is_inactive_and_contains_every_bounded_stage()
    {
        var service = new MarketDataOperationsHealthService(new DatasetWorkerAdmissionRegistry());

        var snapshot = service.GetSnapshot();

        snapshot.OverallStatus.Should().Be(MarketDataOperationsStatus.Inactive);
        snapshot.Stages.Keys.Should().BeEquivalentTo(Enum.GetValues<MarketDataOperationStage>());
    }

    [Fact]
    public void Successful_and_failed_progress_is_visible_in_immutable_snapshot()
    {
        var service = new MarketDataOperationsHealthService(new DatasetWorkerAdmissionRegistry());
        var now = DateTime.UtcNow;
        service.Record(new(MarketDataOperationStage.DatabentoAggregation,
            MarketDataOperationOutcome.Completed, MarketOutlookUpdateKind.EsTrade,
            Guid.NewGuid(), now, TimeSpan.FromMilliseconds(3), now.AddMilliseconds(-1)));
        service.Record(new(MarketDataOperationStage.DatabentoAggregation,
            MarketDataOperationOutcome.Failed, MarketOutlookUpdateKind.EsTrade,
            Guid.NewGuid(), now.AddSeconds(1), TimeSpan.FromMilliseconds(7)));

        var detail = service.GetSnapshot().Stages[MarketDataOperationStage.DatabentoAggregation];

        detail.Completed.Should().Be(1);
        detail.Failed.Should().Be(1);
        detail.Status.Should().Be(MarketDataOperationsStatus.Yellow);
        detail.AverageLatency.Should().Be(TimeSpan.FromMilliseconds(5));
        detail.MaximumLatency.Should().Be(TimeSpan.FromMilliseconds(7));
    }

    [Fact]
    public void Open_and_latched_incidents_drive_orange_and_red_without_native_queries()
    {
        var service = new MarketDataOperationsHealthService(new DatasetWorkerAdmissionRegistry());
        service.RecordIncident(Incident(latched: false));
        service.GetSnapshot().OverallStatus.Should().Be(MarketDataOperationsStatus.Orange);

        service.RecordIncident(Incident(latched: true));
        service.GetSnapshot().OverallStatus.Should().Be(MarketDataOperationsStatus.Red);
    }

    [Fact]
    public void Admission_rejections_are_exposed_without_unbounded_identity_dimensions()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var service = new MarketDataOperationsHealthService(admissions);
        admissions.TryAccept(default, 0).Should().BeFalse();

        service.GetSnapshot().RejectedStaleGenerationPublications.Should().Be(1);
    }

    static DatasetIncidentSnapshot Incident(bool latched) => new()
    {
        Dataset = "GLBX.MDP3",
        ValueDate = new(2026, 9, 4),
        IncidentId = Guid.NewGuid(),
        GenerationId = Guid.NewGuid(),
        IsOpen = true,
        ProcessReplacementLatched = latched,
        FailureReason = DatabentoDatasetFailureReason.NativeDrainStalled,
        ObservedOnUtc = DateTime.UtcNow
    };
}
