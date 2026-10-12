


namespace TomasAI.IFM.Framework.Storage.Benchmarks.CommandLog;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
public sealed record ScyllaCommandLogBenchmarkRecord(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    byte[] MessagePackCommandData);
