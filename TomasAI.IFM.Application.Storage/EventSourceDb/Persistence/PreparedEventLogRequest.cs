using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

internal sealed record PreparedEventLogRequest(
    string EventStream,
    long EventStreamId,
    Guid CommandId,
    IReadOnlyList<PreparedEventLogEntry> Events,
    long? ExpectedStreamVersion,
    DateTime EventTimestampUtc,
    int PayloadBytes,
    CommandAuditEnvelope? CommandAudit);
