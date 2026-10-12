


namespace TomasAI.IFM.Framework.Storage.Benchmarks.CommandLog;

public sealed record PostgresCommandLogBenchmarkRecord(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    string JsonCommandData);
