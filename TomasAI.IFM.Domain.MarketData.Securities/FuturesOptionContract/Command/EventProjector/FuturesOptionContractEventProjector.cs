using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.EventProjector;

public sealed class FuturesOptionContractEventProjector(
    ICommandActorContext<FuturesOptionContractCommandActor> actorContext,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesOptionContractCommandActor>(
        actorContext.DurableReplayQueue, actorContext.DbEventSource,
        actorContext.BlackboardService, actorContext.Logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesOptionContractAddedEvent, FuturesOptionContractAddedCompleteEvent, FuturesOptionContractAddedFailEvent, FuturesOptionContractEntityId>(
            e => string.Equals(actorContext.DbFactory.SecuritiesDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ValueTask.CompletedTask : actorContext.DbFactory.InsertFuturesOptionContractAsync(e.Contract, actorContext.ActorService))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token)),
        Describe<FuturesOptionContractsAddedEvent, FuturesOptionContractsAddedCompleteEvent, FuturesOptionContractsAddedFailEvent, FuturesOptionContractsEntityId>(
            e => string.Equals(actorContext.DbFactory.SecuritiesDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ValueTask.CompletedTask : actorContext.DbFactory.InsertFuturesOptionContractsAsync(e.Contracts, actorContext.ActorService))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token)),
        Describe<FuturesOptionContractChangedEvent, FuturesOptionContractChangedCompleteEvent, FuturesOptionContractChangedFailEvent, FuturesOptionContractEntityId>(
            e => string.Equals(actorContext.DbFactory.SecuritiesDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ValueTask.CompletedTask : actorContext.DbFactory.UpdateFuturesOptionContractAsync(e.OriginalContractId, e.Contract, actorContext.ActorService))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token)),
        Describe<FuturesOptionContractRemovedEvent, FuturesOptionContractRemovedCompleteEvent, FuturesOptionContractRemovedFailEvent, FuturesOptionContractEntityId>(
            e => string.Equals(actorContext.DbFactory.SecuritiesDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ValueTask.CompletedTask : actorContext.DbFactory.DeleteFuturesOptionContractAsync(e.ContractId))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token))
    ];

    static async Task RebuildAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, IEvent source, CancellationToken token)
    {
        var transactions = context.Container.Resolve<IPostgresEventTransaction>();
        await transactions.ExecuteOnceAsync(async (transaction,cancellation) =>
        {
            await SecuritiesContractAtomicPersistence.MaterializeAsync(context.DbFactory.SecuritiesDb,transaction,source,cancellation).ConfigureAwait(false);
            return true;
        },token).ConfigureAwait(false);
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
