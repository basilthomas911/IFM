using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Validation;
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
        
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.BarSignalProjector.StartAsync(context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context)
        
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.BarSignalProjector.StopAsync();
    }

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [PublishFuturesTradeSessionBarCommand.Verb] = static message =>
                message.AsCommand<PublishFuturesTradeSessionBarCommand>()!
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

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(PublishFuturesTradeSessionBarCommand)] = static command =>
            {
                var publish = (PublishFuturesTradeSessionBarCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(publish.CommandId, publish.CommandName)
                    .ValidateEntityId(publish.EntityId, publish.CommandName)
                    .ValidatePublishBar(publish);
                return errors;
            }
        };

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        IActorState state,
        ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesTradeSessionBarSignalCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand,
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor>,
        FuturesTradeSessionBarSignalCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand,
            ICommandActorContext<FuturesTradeSessionBarSignalCommandActor>,
            FuturesTradeSessionBarSignalCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(PublishFuturesTradeSessionBarCommand)] = static (command, _, state) =>
                ((PublishFuturesTradeSessionBarCommand)command).Execute(state)
        };

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        ICommand command) 
    {
        ArgumentNullException.ThrowIfNull(context);
        return await context.BarSignalRepository.LoadStateAsync(command).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) 
    {
        ArgumentNullException.ThrowIfNull(context);
        return await context.BarSignalRepository
            .LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command) 
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.BarSignalRepository.SaveStateAsync(
            context, (FuturesTradeSessionBarSignalCommandState)state, command);
    }

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken) 
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.BarSignalRepository.SaveStateAsync(
            context, (FuturesTradeSessionBarSignalCommandState)state, command, cancellationToken);
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception ex)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ex);
        global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write48(context.Logger, ex, threadId, command?.CommandId, ex.GetType().Name);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? PublishFuturesTradeSessionBarCommand.ErrorId,
                $"BAR.EXCEPTION;ExceptionType={ex.GetType().Name};{ex.Message}"));
    }
}
