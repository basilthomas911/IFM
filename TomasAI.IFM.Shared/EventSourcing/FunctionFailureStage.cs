using MessagePack;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Identifies the Function lifecycle stage that produced a failed response.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The byte-backed enum is an existing persisted/wire contract; preserve its representation.")]
public enum FunctionFailureStage : byte
{
    Unknown = 0,
    Parsing = 1,
    Validation = 2,
    Loading = 3,
    Execution = 4,
    Projection = 5,
    Persistence = 6
}
