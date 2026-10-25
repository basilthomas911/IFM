using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Local lifecycle meaning of a Function event-map callback; never serialized on the wire.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The byte-backed enum is an existing persisted/wire contract; preserve its representation.")]
public enum FunctionEventPhase : byte
{
    Outcome,
    Committed,
    Replayed
}
