using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

/// <summary>
/// Exposes the readonly Market Outlook Snapshot context and publishes component
/// changes from closed-generic source actor contexts.
/// </summary>
public static class MarketOutlookSnapshotRealtimeContextExtensions
{
    extension(IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IMarketOutlookSnapshotRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IMarketOutlookSnapshotRealtimeContext, nameof(context))!;

        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;

        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<MarketOutlookSnapshotRealtimeActor> Logger => context.DomainContext.Logger;
    }

    /// <summary>Forwards one eligible component source event to the Market Outlook command aggregate.</summary>
    internal static async ValueTask ObserveAsync(
        this MarketOutlookComponentChangedRealtimeEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        var command = new ObserveMarketOutlookComponentCommand(
            source.EntityId,
            source.Id,
            source.EventId,
            source.ReceivedOn,
            source.EventSource,
            source.FuturesRsiSignal,
            source.FuturesTdiSignal,
            source.FuturesItiSignal,
            source.VixFuturesPrice)
        {
            CommandId = source.Id,
            Subject = new ActorSubject(
                ActorType.Command,
                ObserveMarketOutlookComponentCommand.Actor,
                ObserveMarketOutlookComponentCommand.Verb,
                source.EntityId.Format())
        };
        var result = await context.RequestAsync<ObserveMarketOutlookComponentCommand, MarketOutlookEntityId>(
            command).ConfigureAwait(false);
        if (result?.Success != true)
            throw new InvalidOperationException(
                result?.ErrorMessage ?? "Market Outlook component observation failed.");
    }

    /// <summary>Reconciles persisted inputs and forwards the EOD publication boundary to the command aggregate.</summary>
    internal static async ValueTask PublishAsync(
        this MarketOutlookEodUpdatedRealtimeEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(source.FuturesEodData.Symbol, "ES", StringComparison.Ordinal))
            return;

        var db = context.DbFactory.MarketDataDb;
        var eodTask = context.GetFuturesEodDataAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate).AsTask();
        var rsiTask = db.GetLastFuturesRsiSignalAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            FuturesTradeSignalPrerequisites.SignalTimePeriod,
            FuturesIntradaySignalActivationProfile.RsiPeriodLength);
        var tdiTask = db.GetLastFuturesTdiSignalAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            FuturesTradeSignalPrerequisites.SignalTimePeriod,
            FuturesTdiConfiguration.StandardConfigurationId);
        var directionTask = db.GetLastFuturesItiSignalTrendDirectionChangeAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate);
        var extremeTask = db.GetLastFuturesItiSignalTrendExtremeChangeAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate);
        var reversalTask = db.GetLastFuturesItiSignalTrendReversalChangeAsync(
            source.EntityId.ContractId,
            source.EntityId.ValueDate);
        var vixTask = context.GetVixFuturesEodDataClosePriceAsync(source.EntityId.ValueDate).AsTask();
        await Task.WhenAll(
            eodTask,
            rsiTask,
            tdiTask,
            directionTask,
            extremeTask,
            reversalTask,
            vixTask).ConfigureAwait(false);

        var command = new PublishMarketOutlookSnapshotCommand(
            source.EntityId,
            source.Id,
            source.EventId,
            source.ReceivedOn,
            await eodTask.ConfigureAwait(false) ?? source.FuturesEodData,
            await rsiTask.ConfigureAwait(false),
            await tdiTask.ConfigureAwait(false),
            new FuturesItiSignalDataReadModel(
                await directionTask.ConfigureAwait(false),
                await extremeTask.ConfigureAwait(false),
                await reversalTask.ConfigureAwait(false)),
            await vixTask.ConfigureAwait(false))
        {
            CommandId = source.Id,
            Subject = new ActorSubject(
                ActorType.Command,
                PublishMarketOutlookSnapshotCommand.Actor,
                PublishMarketOutlookSnapshotCommand.Verb,
                source.EntityId.Format())
        };
        var result = await context.RequestAsync<PublishMarketOutlookSnapshotCommand, MarketOutlookEntityId>(
            command).ConfigureAwait(false);
        if (result?.Success != true)
            throw new InvalidOperationException(
                result?.ErrorMessage ?? "Market Outlook snapshot publication failed.");
    }

    /// <summary>Acknowledges projection of a component checkpoint without retaining realtime state.</summary>
    internal static ValueTask CompleteAsync(
        this MarketOutlookComponentObservedCompleteEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.CompletedTask;
    }

    /// <summary>Publishes the existing UI notification after the finalized snapshot projection completes.</summary>
    internal static async ValueTask CompleteAsync(
        this MarketOutlookSnapshotPublishedCompleteEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        var notification = new MarketOutlookUpdatedNotifyEvent
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                MarketOutlookUpdatedNotifyEvent.Actor,
                MarketOutlookUpdatedNotifyEvent.Verb,
                source.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = source.EntityId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = source.EventName,
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = source.MarketOutlook
        };
        await context.SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(notification)
            .ConfigureAwait(false);
    }

    /// <summary>Logs a failed component projection without creating realtime state or a reply.</summary>
    internal static ValueTask FailAsync(
        this MarketOutlookComponentObservedFailEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        context.Logger.LogError(
            "Market Outlook component projection failed for {EntityId}: {ErrorMessage}",
            source.EntityId.Format(),
            source.ErrorMessage);
        return ValueTask.CompletedTask;
    }

    /// <summary>Logs a failed snapshot projection without publishing a frontend notification.</summary>
    internal static ValueTask FailAsync(
        this MarketOutlookSnapshotPublishedFailEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        context.Logger.LogError(
            "Market Outlook snapshot projection failed for {EntityId}: {ErrorMessage}",
            source.EntityId.Format(),
            source.ErrorMessage);
        return ValueTask.CompletedTask;
    }

    /// <summary>Publishes an RSI component change from its closed-generic source actor context.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesRsiSignalGeneratedCompleteEvent source)
        where TActor : IActor =>
        PublishMarketOutlookComponentAsync(
            context,
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            rsi: source.FuturesRsiSignal);

    /// <summary>Publishes a TDI component change from its closed-generic source actor context.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesTdiSignalGeneratedCompleteEvent source)
        where TActor : IActor =>
        PublishMarketOutlookComponentAsync(
            context,
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            tdi: source.FuturesTdiSignal);

    /// <summary>Publishes an ITI component change from its closed-generic source actor context.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesItiSignalGeneratedCompleteEvent source)
        where TActor : IActor =>
        PublishMarketOutlookComponentAsync(
            context,
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            iti: source.FuturesItiSignal,
            vixFuturesPrice: Convert.ToDecimal(source.VixFuturesPrice));

    static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        IEventActorContext<TActor> context,
        string contractId,
        DateOnly valueDate,
        Guid commandId,
        string? aggregateId,
        string eventSource,
        FuturesRsiSignalReadModel? rsi = null,
        FuturesTdiSignalReadModel? tdi = null,
        FuturesItiSignalV2ReadModel? iti = null,
        decimal vixFuturesPrice = 0)
        where TActor : IActor
    {
        ArgumentNullException.ThrowIfNull(context);

        var entityId = new MarketOutlookEntityId(contractId, valueDate);
        var changed = new MarketOutlookComponentChangedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                MarketOutlookComponentChangedRealtimeEvent.Actor,
                MarketOutlookComponentChangedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = commandId == Guid.Empty ? Guid.NewGuid() : commandId,
            AggregateId = aggregateId ?? string.Empty,
            EventSource = eventSource,
            ReceivedOn = DateTime.UtcNow,
            FuturesRsiSignal = rsi,
            FuturesTdiSignal = tdi,
            FuturesItiSignal = iti,
            VixFuturesPrice = vixFuturesPrice
        };
        return context.SendAsync<MarketOutlookComponentChangedRealtimeEvent, MarketOutlookEntityId>(changed);
    }
}
