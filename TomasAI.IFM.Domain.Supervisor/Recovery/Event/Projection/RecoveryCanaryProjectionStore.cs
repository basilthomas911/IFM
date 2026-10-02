using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;

public sealed record RecoveryCanaryProjection(Guid CorrelationId, Guid GenerationId,
    DateOnly ValueDate, string Dataset, DateTime ProjectedUtc);

/// <summary>Bounded, process-local projection of side-effect-free Supervisor canary events.</summary>
public sealed class RecoveryCanaryProjectionStore : ISupervisorProjectorMetricsSource
{
    const int Capacity = 256;
    readonly object gate = new();
    readonly Dictionary<Guid, RecoveryCanaryProjection> values = [];
    readonly Queue<Guid> order = new();
    bool running;
    long projected;
    DateTime updatedUtc;

    public string SupervisorProjectorKey => "Supervisor/RecoveryCanary";

    public void SetRunning(bool value)
    {
        lock (gate) { running = value; updatedUtc = DateTime.UtcNow; }
    }

    public void Project(RecoveryCanaryAcceptedEvent message)
    {
        if (message.CorrelationId == Guid.Empty || message.GenerationId == Guid.Empty
            || message.ValueDate == default || string.IsNullOrWhiteSpace(message.Dataset)
            || message.Dataset.Length > 64)
            throw new InvalidDataException("Recovery canary identity is invalid.");
        lock (gate)
        {
            if (!running) throw new InvalidOperationException("Recovery canary projector is stopped.");
            if (!values.ContainsKey(message.CorrelationId)) order.Enqueue(message.CorrelationId);
            updatedUtc = DateTime.UtcNow;
            values[message.CorrelationId] = new(message.CorrelationId, message.GenerationId,
                message.ValueDate, message.Dataset, updatedUtc);
            projected++;
            while (order.Count > Capacity) values.Remove(order.Dequeue());
        }
    }

    public bool TryGet(Guid correlationId, Guid generationId, DateOnly valueDate,
        string dataset, out RecoveryCanaryProjection projection)
    {
        lock (gate)
        {
            if (running && values.TryGetValue(correlationId, out var current)
                && current.GenerationId == generationId && current.ValueDate == valueDate
                && string.Equals(current.Dataset, dataset, StringComparison.Ordinal))
            {
                projection = current;
                return true;
            }
            projection = default!;
            return false;
        }
    }

    public SupervisorProjectorSnapshot CaptureSupervisorSnapshot()
    {
        lock (gate)
            return new(RecoveryCanaryCommand.Actor, RecoveryCanaryAcceptedEvent.Actor,
                "Supervisor recovery canary JetStream", "Supervisor recovery canary replay",
                running, projected, projected, updatedUtc, running ? string.Empty : "Projector stopped.");
    }
}
