using TomasAI.IFM.Domain.Supervisor.Recovery;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class RecoveryCanaryProjectionStoreTests
{
    [Fact]
    public void Projection_requires_exact_generation_value_date_and_dataset()
    {
        var store = new RecoveryCanaryProjectionStore();
        store.SetRunning(true);
        var eventData = NewEvent();
        store.Project(eventData);

        Assert.True(store.TryGet(eventData.CorrelationId, eventData.GenerationId,
            eventData.ValueDate, eventData.Dataset, out _));
        Assert.False(store.TryGet(eventData.CorrelationId, Guid.NewGuid(),
            eventData.ValueDate, eventData.Dataset, out _));
        Assert.False(store.TryGet(eventData.CorrelationId, eventData.GenerationId,
            eventData.ValueDate.AddDays(1), eventData.Dataset, out _));
        Assert.False(store.TryGet(eventData.CorrelationId, eventData.GenerationId,
            eventData.ValueDate, "OTHER", out _));
        store.SetRunning(false);
        Assert.False(store.TryGet(eventData.CorrelationId, eventData.GenerationId,
            eventData.ValueDate, eventData.Dataset, out _));
    }

    [Fact]
    public void Projection_evicts_oldest_correlation_when_capacity_is_reached()
    {
        var store = new RecoveryCanaryProjectionStore();
        store.SetRunning(true);
        var first = NewEvent();
        store.Project(first);
        for (var index = 0; index < 256; index++) store.Project(NewEvent());

        Assert.False(store.TryGet(first.CorrelationId, first.GenerationId,
            first.ValueDate, first.Dataset, out _));
        Assert.Equal(257, store.CaptureSupervisorSnapshot().RecoveryEventsDiscovered);
    }

    private static RecoveryCanaryAcceptedEvent NewEvent() => new()
    {
        CorrelationId = Guid.NewGuid(),
        GenerationId = Guid.NewGuid(),
        ValueDate = new DateOnly(2026, 9, 30),
        Dataset = "GLBX.MDP3"
    };
}
