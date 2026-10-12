using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.EventProjector;

public sealed class FuturesContractEventProjector(
    ICommandActorContext<FuturesContractCommandActor> actorContext,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesContractCommandActor>(
        actorContext.DurableReplayQueue, actorContext.DbEventSource,
        actorContext.BlackboardService, actorContext.Logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesContractAddedEvent, FuturesContractAddedCompleteEvent, FuturesContractAddedFailEvent, FuturesContractId>(
            e => string.Equals(actorContext.DbFactory.SecuritiesDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ValueTask.CompletedTask : new ValueTask(actorContext.DbFactory.SecuritiesDb.InsertFuturesContractAsync(e.Contract)))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token)),
        Describe<FuturesContractChangedEvent, FuturesContractChangedCompleteEvent, FuturesContractChangedFailEvent, FuturesContractId>(
            e => string.Equals(actorContext.DbFactory.SecuritiesDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ValueTask.CompletedTask : new ValueTask(actorContext.DbFactory.SecuritiesDb.UpdateFuturesContractAsync(e.OriginalContractId, e.Contract)))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token)),
        Describe<FuturesContractRemovedEvent, FuturesContractRemovedCompleteEvent, FuturesContractRemovedFailEvent, FuturesContractId>(
            e => string.Equals(actorContext.DbFactory.SecuritiesDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? ValueTask.CompletedTask : new ValueTask(actorContext.DbFactory.SecuritiesDb.DeleteFuturesContractAsync(e.ContractId)))
            .WithReadModelRebuild((source,token) => RebuildAsync(actorContext,source,token))
    ];

    static async Task RebuildAsync(ICommandActorContext<FuturesContractCommandActor> context, IEvent source, CancellationToken token)
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
