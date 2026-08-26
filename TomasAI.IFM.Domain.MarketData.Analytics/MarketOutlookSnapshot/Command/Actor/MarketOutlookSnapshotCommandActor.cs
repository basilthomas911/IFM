using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;

/// <summary>
/// Owns the authoritative event-sourced Market Outlook working state and published snapshots.
/// </summary>
public sealed class MarketOutlookSnapshotCommandActor(
    ICommandActorContext<MarketOutlookSnapshotCommandActor> actorContext)
    : BaseEventSourceCommandActor<MarketOutlookSnapshotCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the stable command actor mailbox name.</summary>
    public const string ActorName = ObserveMarketOutlookComponentCommand.Actor;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context)
        => await context.EventProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context)
        => context.EventProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName })
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return message.Subject.Verb switch
        {
            ObserveMarketOutlookComponentCommand.Verb =>
                message.AsCommand<ObserveMarketOutlookComponentCommand>()!,
            PublishMarketOutlookSnapshotCommand.Verb =>
                message.AsCommand<PublishMarketOutlookSnapshotCommand>()!,
            _ => throw new InvalidOperationException(
                $"Unable to resolve {ActorName} command from {message.Subject}.")
        };
    }

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateEnvelope(command);
        switch (command)
        {
            case ObserveMarketOutlookComponentCommand observe:
                ValidateObserve(observe);
                break;
            case PublishMarketOutlookSnapshotCommand publish:
                ValidatePublish(publish);
                break;
            default:
                throw new InvalidOperationException($"Unsupported Market Outlook command {command.GetType().Name}.");
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        IActorState state,
        ICommand command)
    {
        var marketOutlookState = (MarketOutlookSnapshotCommandState)state;
        return ValueTask.FromResult(command switch
        {
            ObserveMarketOutlookComponentCommand observe => observe.Execute(marketOutlookState),
            PublishMarketOutlookSnapshotCommand publish => publish.Execute(marketOutlookState),
            _ => throw new InvalidOperationException($"Unsupported Market Outlook command {command.GetType().Name}.")
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
        => await context.StateRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken)
        => await context.StateRepository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command)
        => context.StateRepository.SaveStateAsync(
            context,
            (MarketOutlookSnapshotCommandState)state,
            command);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken)
        => context.StateRepository.SaveStateAsync(
            context,
            (MarketOutlookSnapshotCommandState)state,
            command,
            cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(
            command?.ErrorCode ?? ObserveMarketOutlookComponentCommand.ErrorId,
            exception.Message));

    static void ValidateEnvelope(ICommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new ArgumentException("A Market Outlook command ID is required.");
        var entityId = command switch
        {
            ObserveMarketOutlookComponentCommand observe => observe.EntityId,
            PublishMarketOutlookSnapshotCommand publish => publish.EntityId,
            _ => throw new InvalidOperationException($"Unsupported Market Outlook command {command.GetType().Name}.")
        };
        if (command.Subject.EntityId != entityId.Format())
            throw new ArgumentException("The Market Outlook command subject must match its entity identity.");
    }

    static void ValidateObserve(ObserveMarketOutlookComponentCommand command)
    {
        if (command.SourceEventId == Guid.Empty || command.SourceEventTimestamp == default)
            throw new ArgumentException("A stable Market Outlook source identity and timestamp are required.");
        if (command.ComponentCount == 0 || command.ComponentCount > 2
            || command.ComponentCount == 2
                && (command.FuturesItiSignal is null || command.VixFuturesPrice <= 0))
            throw new ArgumentException("A component command must contain one component, or ITI with its VX price.");
        if (command.FuturesRsiSignal is { } rsi
            && (rsi.ContractId != command.EntityId.ContractId
                || rsi.ValueDate != command.EntityId.ValueDate
                || rsi.TimePeriod != FuturesTradeSignalPrerequisites.SignalTimePeriod
                || rsi.PeriodLength != FuturesIntradaySignalActivationProfile.RsiPeriodLength))
            throw new ArgumentException("The RSI component is not eligible for this Market Outlook entity.");
        if (command.FuturesTdiSignal is { } tdi
            && (tdi.ContractId != command.EntityId.ContractId
                || tdi.ValueDate != command.EntityId.ValueDate
                || tdi.TimePeriod != FuturesTradeSignalPrerequisites.SignalTimePeriod
                || tdi.ConfigurationId != FuturesTdiConfiguration.StandardConfigurationId))
            throw new ArgumentException("The TDI component is not eligible for this Market Outlook entity.");
        if (command.FuturesItiSignal is { } iti
            && (iti.ContractId != command.EntityId.ContractId
                || iti.ValueDate != command.EntityId.ValueDate
                || iti.TimePeriod != TimeFrameType.Daily
                || iti.IntrinsicTimeMode is not (IntrinsicTimeModeType.TrendDirectionChanged
                    or IntrinsicTimeModeType.TrendExtremeChanged
                    or IntrinsicTimeModeType.TrendReversalChanged)))
            throw new ArgumentException("The ITI component is not eligible for this Market Outlook entity.");
    }

    static void ValidatePublish(PublishMarketOutlookSnapshotCommand command)
    {
        if (command.SourceEventId == Guid.Empty || command.SourceEventTimestamp == default)
            throw new ArgumentException("A stable Market Outlook EOD source identity and timestamp are required.");
        if (command.FuturesEodData.ContractId != command.EntityId.ContractId
            || command.FuturesEodData.ValueDate != command.EntityId.ValueDate
            || !string.Equals(command.FuturesEodData.Symbol, "ES", StringComparison.Ordinal))
            throw new ArgumentException("The Market Outlook EOD input must be the matching ES contract and date.");
    }
}
