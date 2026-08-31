using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;
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

    /// <inheritdoc />
    protected override IReadOnlyList<ValidationError>? GetCommandValidationErrors(ICommand command) =>
        _validationMap.TryGetValue(command.GetType(), out var validator)
            ? validator(command)
            : throw new InvalidOperationException(
                $"Unable to validate {ActorName} commands from message: {command.Subject}");

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(ObserveMarketOutlookComponentCommand)] = static command =>
        {
            var observe = (ObserveMarketOutlookComponentCommand)command;
            var errors = new List<ValidationError>()
                .ValidateCommandId(observe.CommandId, observe.CommandName)
                .ValidateEntityId(observe.EntityId, observe.CommandName);
            ValidateEnvelope(errors, observe, observe.EntityId.Format());
            ValidateObserve(errors, observe);
            return errors;
        },
        [typeof(PublishMarketOutlookSnapshotCommand)] = static command =>
        {
            var publish = (PublishMarketOutlookSnapshotCommand)command;
            var errors = new List<ValidationError>()
                .ValidateCommandId(publish.CommandId, publish.CommandName)
                .ValidateEntityId(publish.EntityId, publish.CommandName);
            ValidateEnvelope(errors, publish, publish.EntityId.Format());
            ValidatePublish(errors, publish);
            return errors;
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

    static void ValidateEnvelope(List<ValidationError> errors, ICommand command, string entityId)
    {
        if (command.CommandId == Guid.Empty)
            errors.Add(new("A Market Outlook command ID is required."));
        if (command.Subject.EntityId != entityId)
            errors.Add(new("The Market Outlook command subject must match its entity identity."));
    }

    static void ValidateObserve(List<ValidationError> errors, ObserveMarketOutlookComponentCommand command)
    {
        if (command.SourceEventId == Guid.Empty || command.SourceEventTimestamp == default)
            errors.Add(new("A stable Market Outlook source identity and timestamp are required."));
        if (command.ComponentCount == 0)
            errors.Add(new("A component command must contain at least one supplied component."));
    }

    static void ValidatePublish(List<ValidationError> errors, PublishMarketOutlookSnapshotCommand command)
    {
        if (command.SourceEventId == Guid.Empty || command.SourceEventTimestamp == default)
            errors.Add(new("A stable Market Outlook EOD source identity and timestamp are required."));
        if (command.FuturesEodData.ContractId != command.EntityId.ContractId
            || command.FuturesEodData.ValueDate != command.EntityId.ValueDate
            || !string.Equals(command.FuturesEodData.Symbol, "ES", StringComparison.Ordinal))
            errors.Add(new("The Market Outlook EOD input must be the matching ES contract and date."));
    }
}
