


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
public enum EventLogWriteMode : byte
{
    Sequential = 0,
    BinaryCopy = 1
}
