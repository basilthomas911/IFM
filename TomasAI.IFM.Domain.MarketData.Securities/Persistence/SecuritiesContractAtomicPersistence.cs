using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;
/// <summary>Commits accepted contract events and canonical, symbol and reference projections together.</summary>
internal static class SecuritiesContractAtomicPersistence
{
    internal static async Task SaveAsync(ISecuritiesDbContext database, IPostgresEventTransaction transactions,
        DomainEventCollection pending, ICommand command, long committedVersion, CancellationToken token, IEvent? snapshot = null)
    {
        await transactions.ExecuteAsync(async (transaction,cancellation) =>
        {
            var version = committedVersion;
            if (version == 0 && command.StreamId is SecuritiesCatalogStreams.Futures or SecuritiesCatalogStreams.Options)
            {
                if (await database.HasLegacyCatalogHistoryAsync(transaction,
                    [SecuritiesCatalogStreams.Futures, SecuritiesCatalogStreams.Options],
                    ["Command.FuturesContractCommand.", "Command.FuturesOptionContractCommand."], cancellation)
                    .ConfigureAwait(false))
                    throw new InvalidOperationException("Capture the verified PostgreSQL Securities baseline before accepting catalog commands.");
            }
            foreach (var source in pending)
            {
                await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId,command.CommandId,source,version++),cancellation).ConfigureAwait(false);
                await SecuritiesCatalogProjection.ApplyAsync(database,transaction,source,cancellation).ConfigureAwait(false);
            }
            if (snapshot is not null)
                await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, command.CommandId, snapshot, version), cancellation).ConfigureAwait(false);
            return true;
        },token).ConfigureAwait(false);
    }

    /// <summary>Submits already committed facts whose projector obligations can survive failed delivery.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The committed command remains accepted and the projector can recover its durable obligations.")]
    internal static async ValueTask SubmitCommittedAsync(IEventProjector projector, DomainEventCollection pending,
        ICommand command, ILogger logger)
    {
        try
        {
            await projector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            SubmissionFailure(logger, command.CommandId, exception);
        }
    }

    private static readonly Action<ILogger, Guid, Exception?> SubmissionFailure = LoggerMessage.Define<Guid>(
        LogLevel.Error, new EventId(1, nameof(SubmitCommittedAsync)),
        "Securities command {CommandId} committed but projector submission failed. The committed events remain eligible for recovery.");

}
