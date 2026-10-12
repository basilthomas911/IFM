using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;

/// <summary>Sends a snapshot through the legacy event-sourced command path.</summary>
public sealed class ActorMarketOutlookSnapshotCommandWriter
    : IMarketOutlookSnapshotCommandWriter
{
    static readonly ActorMailboxId PublisherId = new(
        ActorType.Command,
        InsertMarketOutlookSnapshotCommand.Actor);
    IActorProducer? producer;
    readonly IActorSupervisor supervisor;
    readonly MarketOutlookSnapshotPersistencePolicy persistencePolicy;

    public ActorMarketOutlookSnapshotCommandWriter(IActorSupervisor supervisor)
        : this(supervisor, MarketOutlookSnapshotPersistencePolicy.Legacy)
    {
    }

    public ActorMarketOutlookSnapshotCommandWriter(
        IActorSupervisor supervisor,
        MarketOutlookSnapshotPersistencePolicy persistencePolicy)
    {
        this.supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        this.persistencePolicy = persistencePolicy
            ?? throw new ArgumentNullException(nameof(persistencePolicy));
    }

    public async ValueTask PublishAsync(
        MarketOutlookUpdate update,
        MarketOutlookReadModel snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(update);
        var durableSnapshot = snapshot with
        {
            SnapshotSource = persistencePolicy.SnapshotSource
        };
        var command = new InsertMarketOutlookSnapshotCommand(durableSnapshot) with
        {
            CommandId = update.UpdateId == Guid.Empty ? Guid.NewGuid() : update.UpdateId
        };
        var result = await (producer ??= supervisor.GetProducer(PublisherId))
            .RequestAsync<InsertMarketOutlookSnapshotCommand, MarketOutlookEntityId, GuidResult>(
                command.Subject,
                command,
                command.EntityId,
                cancellationToken)
            .ConfigureAwait(false);
        if (result?.Success != true)
            throw new InvalidOperationException(
                result?.ErrorMessage ?? "Market Outlook snapshot insert command failed.");
    }
}
