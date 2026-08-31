using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Actor;

/// <summary>Owns the durable, event-sourced publication state for completed futures trade-session bars.</summary>
public sealed class FuturesTradeSessionBarSignalCommandActor(
    ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesTradeSessionBarSignalCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Command actor mailbox name.</summary>
    public const string ActorName = PublishFuturesTradeSessionBarCommand.Actor;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context)
        => await context.BarSignalProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context)
        => context.BarSignalProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [PublishFuturesTradeSessionBarCommand.Verb] = message =>
            message.AsCommand<PublishFuturesTradeSessionBarCommand>()
            ?? throw new InvalidOperationException("Unable to deserialize the Publish bar command.")
    };

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
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
        [typeof(PublishFuturesTradeSessionBarCommand)] = static command =>
        {
            var publish = (PublishFuturesTradeSessionBarCommand)command;
            var errors = new List<ValidationError>()
                .ValidateCommandId(publish.CommandId, publish.CommandName)
                .ValidateEntityId(publish.EntityId, publish.CommandName);
            ValidatePublish(errors, publish);
            return errors;
        }
    };

    static void ValidatePublish(List<ValidationError> errors, PublishFuturesTradeSessionBarCommand value)
    {
        if (value.CommandId == Guid.Empty || value.CommandId != value.Bar.ObservationId.Value)
            errors.Add(new("CommandId must equal the deterministic bar identity."));
        if (new FuturesTradeSessionBarEntityIdValidationRules().Execute(value.EntityId).Length != 0)
            errors.Add(new("A valid bar signal entity identity is required."));
        if (new FuturesTradeSessionBarReadModelValidationRules().Execute(value.Bar).Length != 0)
            errors.Add(new("A valid completed futures trade-session bar is required."));
        if (value.EntityId.MarketSeriesIdentity != value.Bar.MarketSeriesIdentity
            || value.EntityId.TimeFrame != value.Bar.TimeFrame
            || value.Subject.EntityId != value.EntityId.Format())
            errors.Add(new("Command routing identity must match the completed bar."));
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        IActorState state,
        ICommand command)
    {
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesTradeSessionBarSignalCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand,
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor>,
        FuturesTradeSessionBarSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand,
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor>,
        FuturesTradeSessionBarSignalCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(PublishFuturesTradeSessionBarCommand)] = static (command, _, state) =>
            ((PublishFuturesTradeSessionBarCommand)command).Execute(state)
    };

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        ICommand command) => await context.BarSignalRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) => await context.BarSignalRepository
            .LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command) => context.BarSignalRepository.SaveStateAsync(
            context, (FuturesTradeSessionBarSignalCommandState)state, command);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken) => context.BarSignalRepository.SaveStateAsync(
            context, (FuturesTradeSessionBarSignalCommandState)state, command, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? PublishFuturesTradeSessionBarCommand.ErrorId,
                exception.Message));
}
