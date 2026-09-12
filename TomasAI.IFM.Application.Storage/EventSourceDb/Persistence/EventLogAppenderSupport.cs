using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

internal sealed record PreparedEventLogEntry(
    int EventNameId,
    IEvent DomainEvent,
    byte[] Payload,
    DurableProjectionRequirement? RequiredProjection);

internal sealed record PreparedEventLogRequest(
    string EventStream,
    long EventStreamId,
    Guid CommandId,
    IReadOnlyList<PreparedEventLogEntry> Events,
    long? ExpectedStreamVersion,
    DateTime EventTimestampUtc,
    int PayloadBytes,
    CommandAuditEnvelope? CommandAudit);

internal static class EventLogAppenderSupport
{
    internal static PreparedEventLogRequest Prepare(
        EventLogAppendRequest request,
        EventLogMessagePackCodec codec,
        EventLogPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EventStream);
        if (request.EventStreamId <= 0) throw new ArgumentOutOfRangeException(nameof(request.EventStreamId));
        if (request.CommandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(request));
        if (request.Events is null || request.Events.Count == 0) throw new ArgumentException("At least one event is required.", nameof(request));
        if (request.Events.Count > options.MaximumEventsPerCommand) throw new ArgumentException("Event command exceeds its configured event limit.", nameof(request));
        if (request.ExpectedStreamVersion is < 0) throw new ArgumentOutOfRangeException(nameof(request.ExpectedStreamVersion));

        var prepared = new PreparedEventLogEntry[request.Events.Count];
        var totalBytes = 0;
        for (var index = 0; index < request.Events.Count; index++)
        {
            var item = request.Events[index] ?? throw new ArgumentException("Event entry cannot be null.", nameof(request));
            if (item.EventNameId <= 0) throw new ArgumentOutOfRangeException(nameof(item.EventNameId));
            ArgumentNullException.ThrowIfNull(item.DomainEvent);
            var payload = codec.Serialize(item.DomainEvent);
            if (payload.Length == 0 || payload.Length > options.MaximumEventPayloadBytes)
                throw new ArgumentException("Event payload is outside its configured size limit.", nameof(request));
            totalBytes = checked(totalBytes + payload.Length);
            if (totalBytes > options.MaximumCommandPayloadBytes)
                throw new ArgumentException("Event command exceeds its configured payload limit.", nameof(request));
            DurableProjectionRequirement? projection = null;
            if (item.DomainEvent is IRequireDurableProjection required)
            {
                projection = required.RequiredProjection;
                ArgumentException.ThrowIfNullOrWhiteSpace(projection.ActorName);
                ArgumentException.ThrowIfNullOrWhiteSpace(projection.ProjectorName);
                if (projection.InitialStage is not (EventProjectorStageType.PublishProcessingEvent or EventProjectorStageType.ApplyProjection))
                    throw new ArgumentException("Invalid initial durable projection stage.", nameof(request));
            }
            prepared[index] = new PreparedEventLogEntry(item.EventNameId, item.DomainEvent, payload, projection);
        }
        return new PreparedEventLogRequest(request.EventStream, request.EventStreamId, request.CommandId, prepared,
            request.ExpectedStreamVersion, DateTime.SpecifyKind(request.EventTimestampUtc, DateTimeKind.Utc), totalBytes,
            request.CommandAudit);
    }

    internal static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql)
        => new(sql, connection, transaction) { CommandTimeout = 3 };

    internal static void Add(NpgsqlCommand command, object value, NpgsqlDbType type)
        => command.Parameters.Add(new NpgsqlParameter { Value = value, NpgsqlDbType = type });

    internal static async Task InsertProjectionMarkerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long eventId,
        DurableProjectionRequirement requirement,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var command = Command(connection, transaction, EventSourceDbSql.TryCreateEventProjectorExecutionState);
        Add(command, eventId, NpgsqlDbType.Bigint);
        Add(command, requirement.ActorName, NpgsqlDbType.Text);
        Add(command, requirement.ProjectorName, NpgsqlDbType.Text);
        Add(command, false, NpgsqlDbType.Boolean);
        Add(command, 0, NpgsqlDbType.Integer);
        Add(command, "Processing", NpgsqlDbType.Text);
        Add(command, requirement.InitialStage.ToString(), NpgsqlDbType.Text);
        Add(command, string.Empty, NpgsqlDbType.Text);
        Add(command, $"{now:o}", NpgsqlDbType.Text);
        Add(command, $"{now:o}", NpgsqlDbType.Text);
        Add(command, now, NpgsqlDbType.TimestampTz);
        if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            throw new InvalidOperationException("The required durable projection marker was not persisted.");
    }
}
