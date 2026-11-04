using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>
/// Creates an assignment aggregate and provides its default replay snapshot.
/// Inherited keys retain the assignment, request fingerprint and audit contract.
/// Later changes remain separate events and are replayed after this event.
/// </summary>
[MessagePackObject]
public sealed record ParameterAssignmentCreatedEvent : ParameterAssignmentChangedEvent
{
    public new const string Actor = "ParameterAssignment";
    public new const string Verb = "ParameterAssignmentCreated";

    [IgnoreMember]
    public override string EventName => nameof(ParameterAssignmentCreatedEvent);
}
