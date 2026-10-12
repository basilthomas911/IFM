using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
/// <summary>Blocks a non-empty workflow stream that has not been explicitly migrated to state snapshots.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This domain failure requires structured reason or stream/revision evidence; generic constructors would create failures without the identity required by its consumer.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The domain exception requires structured reason or stream/revision evidence; generic constructors would omit facts required by its consumer.")]
public sealed class LegacyWorkflowStreamException : InvalidOperationException
{
    /// <summary>Creates a fail-closed legacy-stream error.</summary>
    public LegacyWorkflowStreamException(string streamId, int eventCount, string? detail = null) : base($"Workflow stream '{streamId}' contains {eventCount} event(s) but no safe current snapshot can be " + $"loaded. Explicit migration or archival is required. {detail}".Trim())
    {
        StreamId = streamId;
        EventCount = eventCount;
    }

    /// <summary>Gets the blocked stream identity.</summary>
    public string StreamId { get; }
    /// <summary>Gets the number of events observed by the snapshot load.</summary>
    public int EventCount { get; }
}
