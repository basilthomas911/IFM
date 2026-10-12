using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

internal sealed record PreparedEventLogEntry(
    int EventNameId,
    IEvent DomainEvent,
    byte[] Payload,
    DurableProjectionRequirement? RequiredProjection);
