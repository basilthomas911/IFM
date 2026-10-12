using Newtonsoft.Json;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandLogBenchmark;

public sealed record PostgresCommandLogBenchmarkRecord(
    Guid CommandId,
    string StreamId,
    string ActorName,
    string CommandName,
    DateTime CommandTimestampUtc,
    string JsonCommandData);
