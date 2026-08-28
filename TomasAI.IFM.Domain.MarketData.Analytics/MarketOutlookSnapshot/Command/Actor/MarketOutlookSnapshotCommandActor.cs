using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

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
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [ObserveMarketOutlookComponentCommand.Verb] = message =>
            message.AsCommand<ObserveMarketOutlookComponentCommand>()!,
        [PublishMarketOutlookSnapshotCommand.Verb] = message =>
            message.AsCommand<PublishMarketOutlookSnapshotCommand>()!
    };

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(ObserveMarketOutlookComponentCommand)] = static command =>
        {
            var observe = (ObserveMarketOutlookComponentCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(observe.CommandId, observe.CommandName)
                .ValidateEntityId(observe.EntityId, observe.CommandName)
                .CaptureCommandValidation(() => ValidateEnvelope(observe, observe.EntityId.Format()))
                .CaptureCommandValidation(() => ValidateObserve(observe));
        },
        [typeof(PublishMarketOutlookSnapshotCommand)] = static command =>
        {
            var publish = (PublishMarketOutlookSnapshotCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(publish.CommandId, publish.CommandName)
                .ValidateEntityId(publish.EntityId, publish.CommandName)
                .CaptureCommandValidation(() => ValidateEnvelope(publish, publish.EntityId.Format()))
                .CaptureCommandValidation(() => ValidatePublish(publish));
        }
    };

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        IActorState state,
        ICommand command)
    {
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (MarketOutlookSnapshotCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand,
        ICommandActorContext<MarketOutlookSnapshotCommandActor>,
        MarketOutlookSnapshotCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand,
        ICommandActorContext<MarketOutlookSnapshotCommandActor>,
        MarketOutlookSnapshotCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(ObserveMarketOutlookComponentCommand)] = static (command, _, state) =>
            ((ObserveMarketOutlookComponentCommand)command).Execute(state),
        [typeof(PublishMarketOutlookSnapshotCommand)] = static (command, _, state) =>
            ((PublishMarketOutlookSnapshotCommand)command).Execute(state)
    };

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

    static void ValidateEnvelope(ICommand command, string entityId)
    {
        if (command.CommandId == Guid.Empty)
            throw new ArgumentException("A Market Outlook command ID is required.");
        if (command.Subject.EntityId != entityId)
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
