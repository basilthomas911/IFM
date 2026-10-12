using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Domain.EventSourcing.Command.Deduplication;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.EventSourcing.Command;

/// <summary>Coordinates command audit acceptance and duplicate callers before domain actors execute.</summary>
public sealed class CommandAuditCoordinator : ICommandAuditLogger
{
    readonly IEventSourceActorDbContext database;
    readonly ILogger logger;
    readonly CommandAuditPersistenceOptions options;
    readonly CommandAuditMessagePackCodec codec = new();
    readonly Lazy<CommandDuplicateCoordinator> duplicates = new(
        () => new CommandDuplicateCoordinator(CommandDuplicateCoordinator.ReadConfiguredCapacity()));
    readonly ConcurrentDictionary<Guid, Lazy<Task<bool>>> parsedReservations = new();

    /// <summary>Creates domain command coordination over the supplied persistence contract.</summary>
    /// <param name="database">The event-source persistence contract.</param>
    /// <param name="logger">The domain diagnostic logger.</param>
    /// <param name="options">The audit format and persistence configuration.</param>
    public CommandAuditCoordinator(IEventSourceActorDbContext database,
        ILogger<CommandAuditCoordinator>? logger = null, CommandAuditPersistenceOptions? options = null)
    {
        this.database = database;
        this.logger = logger ?? NullLogger<CommandAuditCoordinator>.Instance;
        this.options = options ?? new();
    }

    /// <summary>Retains a pre-parse audit acceptance for the actor to consume exactly once.</summary>
    /// <param name="command">The parsed command.</param>
    /// <param name="commandTimestamp">The audit timestamp.</param>
    /// <param name="commandData">The serialized legacy payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Completion of the pre-parse audit operation.</returns>
    public async Task InsertCommandLogAsync(ICommand command, DateTime commandTimestamp, string commandData,
        CancellationToken cancellationToken = default)
    {
        var candidate = new Lazy<Task<bool>>(() => duplicates.Value.TryAcceptAsync(command.CommandId,
            token => database.TryInsertCommandLogAsync(command, commandTimestamp, commandData, token), cancellationToken).AsTask());
        var reservation = parsedReservations.GetOrAdd(command.CommandId, candidate);
        try { _ = await reservation.Value.ConfigureAwait(false); }
        catch
        {
            parsedReservations.TryRemove(new KeyValuePair<Guid, Lazy<Task<bool>>>(command.CommandId, reservation));
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CommandAuditReservation> TryReserveAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            if (parsedReservations.TryRemove(command.CommandId, out var prior))
                return new(await prior.Value.WaitAsync(cancellationToken).ConfigureAwait(false));
            bool accepted;
            if (options.WriteMode == CommandAuditWriteMode.SequentialJsonLegacy)
                accepted = await duplicates.Value.TryAcceptAsync(command.CommandId,
                    token => database.TryInsertCommandLogAsync(command, DateTime.UtcNow, JsonConvert.SerializeObject(command), token),
                    cancellationToken).ConfigureAwait(false);
            else
            {
                var envelope = CommandAuditEnvelope.Create(command, codec);
                accepted = await duplicates.Value.TryAcceptAsync(command.CommandId, envelope.RetrySha256,
                    token => database.TryInsertCommandAuditAsync(envelope, token).AsTask(), cancellationToken).ConfigureAwait(false);
            }
            return new(accepted);
        }
        catch (CommandAuditPayloadConflictException exception)
        {
            logger.LogWarning(exception, "Command audit payload conflict for {CommandId} on {Subject} using {WriteMode}",
                command.CommandId, command.Subject, options.WriteMode);
            throw;
        }
        finally
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (elapsed >= 250)
                logger.LogInformation("Slow domain command reservation for {CommandId} on {Subject}: {ElapsedMilliseconds} ms",
                    command.CommandId, command.Subject, elapsed);
        }
    }
}
