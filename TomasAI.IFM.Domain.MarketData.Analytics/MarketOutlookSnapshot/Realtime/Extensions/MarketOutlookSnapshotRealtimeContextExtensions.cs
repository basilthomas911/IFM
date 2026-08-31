using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
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
        var eligibleSource = MarketOutlookComponentEligibility.SelectEligible(
            source,
            out var ineligibleReason);
        if (!MarketOutlookComponentEligibility.IsEligible(eligibleSource, out _))
        {
            context.Logger.LogDebug(
                "Ignoring ineligible Market Outlook component {EventSource} for {EntityId}: {Reason}",
                source.EventSource,
                source.EntityId.Format(),
                ineligibleReason);
            return;
        }
        var command = new ObserveMarketOutlookComponentCommand(
            eligibleSource.EntityId,
            eligibleSource.Id,
            eligibleSource.EventId,
            eligibleSource.ReceivedOn,
            eligibleSource.EventSource,
            eligibleSource.FuturesRsiSignal,
            eligibleSource.FuturesTdiSignal,
            eligibleSource.FuturesItiSignal,
            eligibleSource.VixFuturesPrice,
            eligibleSource.FuturesEmaSignal,
            eligibleSource.FuturesBbSignal)
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
            context.Logger.LogWarning(
                "Market Outlook component observation was rejected for {EntityId}: {ErrorMessage}",
                source.EntityId.Format(),
                result?.ErrorMessage ?? "No command result was returned.");
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

        RegimeDiscoverySignalCacheAdapter.TryGetLatestEma(
            source.EntityId.ContractId,
            TimeFrameType.Daily,
            out var latestEma);
        RegimeDiscoverySignalCacheAdapter.TryGetLatestBb(
            source.EntityId.ContractId,
            TimeFrameType.Daily,
            out var latestBb);

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
            await vixTask.ConfigureAwait(false),
            latestEma,
            latestBb)
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
            context.Logger.LogWarning(
                "Market Outlook snapshot publication was rejected for {EntityId}: {ErrorMessage}",
                source.EntityId.Format(),
                result?.ErrorMessage ?? "No command result was returned.");
    }

    /// <summary>Publishes a revised UI snapshot when a component reprojects an existing EOD outlook.</summary>
    internal static async ValueTask CompleteAsync(
        this MarketOutlookComponentObservedCompleteEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (source.WorkingState.PublishedSnapshot is not { IsValid: true } snapshot)
            return;

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
            MarketOutlook = snapshot
        };
        await context.SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(notification)
            .ConfigureAwait(false);
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

    /// <summary>Publishes one completed Daily EMA family independently.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesEmaSignalGeneratedCompleteEvent source)
        where TActor : IActor
    {
        if (!IsEsSeries(source.Signal.Metadata.SignalKey.MarketSeriesIdentity))
            return ValueTask.CompletedTask;
        return PublishMarketOutlookComponentAsync(
            context,
            source.Signal.Metadata.ContractId,
            source.Signal.Metadata.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            ema: source.Signal);
    }

    /// <summary>Publishes one completed Daily Bollinger family independently.</summary>
    internal static ValueTask PublishMarketOutlookComponentAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesBbSignalGeneratedCompleteEvent source)
        where TActor : IActor
    {
        if (!IsEsSeries(source.Signal.Metadata.SignalKey.MarketSeriesIdentity))
            return ValueTask.CompletedTask;
        return PublishMarketOutlookComponentAsync(
            context,
            source.Signal.Metadata.ContractId,
            source.Signal.Metadata.ValueDate,
            source.CommandId,
            source.AggregateId,
            source.EventName,
            bb: source.Signal);
    }

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
        decimal vixFuturesPrice = 0,
        FuturesEmaSignalReadModel? ema = null,
        FuturesBbSignalReadModel? bb = null)
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
            VixFuturesPrice = vixFuturesPrice,
            FuturesEmaSignal = ema,
            FuturesBbSignal = bb
        };
        var eligible = MarketOutlookComponentEligibility.SelectEligible(changed, out _);
        if (!MarketOutlookComponentEligibility.IsEligible(eligible, out _))
            return ValueTask.CompletedTask;
        return context.SendAsync<MarketOutlookComponentChangedRealtimeEvent, MarketOutlookEntityId>(eligible);
    }

    static bool IsEsSeries(MarketSeriesIdentity series) =>
        series.FuturesSeriesId is { } continuation
            ? string.Equals(continuation.RootSymbol, "ES", StringComparison.OrdinalIgnoreCase)
            : series.Kind == MarketSeriesIdentityKind.Contract
              && series.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase);
}
