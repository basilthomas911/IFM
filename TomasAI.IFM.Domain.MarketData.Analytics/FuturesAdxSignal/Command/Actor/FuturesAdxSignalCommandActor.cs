using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;

/// <summary>
/// Represents an actor responsible for managing futures ADX signal commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesAdxSignalCommandActor"/> is a specialized command actor designed to handle operations
/// related to futures ADX signals. It processes the generate command, validates the command, and manages the actor's state.
/// This actor relies on an event-sourced repository for state persistence and uses dependency injection to resolve
/// required services.</remarks>
/// <param name="dbEventSource">The event source database context used for logging and persisting command events.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class FuturesAdxSignalCommandActor(
    ICommandActorContext<FuturesAdxSignalCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesAdxSignalCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesAdxSignalCommandContext ActorContext =>
        IsArgumentNull.Named(Context as IFuturesAdxSignalCommandContext, nameof(Context))!;

    public const string ActorName = "FuturesAdxSignalCommand";
    IEventProjector<FuturesAdxSignalCommandActor> EventProjector => ActorContext.EventProjector;
    IEventSourceActorStateRepository<FuturesAdxSignalCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesAdxSignalCommandActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<FuturesAdxSignalCommandState>>());
        await EventProjector.StartAsync(context).ConfigureAwait(false);
    }

    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesAdxSignalCommandActor> context)
        => await EventProjector.StopAsync().ConfigureAwait(false);

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a command instance for the specified actor context.
    /// </summary>
    /// <param name="context">The actor context used to resolve and process the command. Cannot be null.</param>
    /// <param name="message">The NATS message containing the command data to be parsed.</param>
    /// <returns>An <see cref="ICommand"/> instance representing the parsed command from the message.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known command for the actor.</exception>
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesAdxSignalCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from command verb strings to delegate functions that parse a NATS message into the
    /// corresponding command instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [StartFuturesAdxSignalCommand.Verb] = msg => msg.AsCommand<StartFuturesAdxSignalCommand>()!,
        [StopFuturesAdxSignalCommand.Verb] = msg => msg.AsCommand<StopFuturesAdxSignalCommand>()!,
        [GenerateFuturesAdxSignalCommand.Verb] = msg => msg.AsCommand<GenerateFuturesAdxSignalCommand>()!,
        [GenerateFuturesAdxDailySignalCommand.Verb] = msg => msg.AsCommand<GenerateFuturesAdxDailySignalCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of <see cref="FuturesAdxSignalCommandState"/>. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, IActorState state, ICommand command)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);
        var adxSignalState = IsArgumentNull.Set((state as FuturesAdxSignalCommandState)!);
        var receiveFunc = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveFunc.Invoke(command, dispatchContext, adxSignalState));
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding futures ADX signal
    /// command logic on a given state.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<FuturesAdxSignalCommandActor>, FuturesAdxSignalCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<FuturesAdxSignalCommandActor>, FuturesAdxSignalCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(StartFuturesAdxSignalCommand)] = (cmd, context, state) => ((StartFuturesAdxSignalCommand)cmd).Execute(state),
        [typeof(StopFuturesAdxSignalCommand)] = (cmd, context, state) => ((StopFuturesAdxSignalCommand)cmd).Execute(state),
        [typeof(GenerateFuturesAdxSignalCommand)] = (cmd, context, state) => (cmd as GenerateFuturesAdxSignalCommand)!.Execute(state),
        [typeof(GenerateFuturesAdxDailySignalCommand)] = (cmd, context, state) => (cmd as GenerateFuturesAdxDailySignalCommand)!.Execute(state)
    };

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <param name="context">The context in which the command is being executed.</param>
    /// <param name="threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name="cmd">The command to be validated. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command, CancellationToken.None);

    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        _=command.GetType();
        ValidateMappedCommand(command, _validationMap);
    }

    /// <summary>
    /// Provides a mapping from command type names to their corresponding validation functions.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>()
        {
            [typeof(StartFuturesAdxSignalCommand)] = cmd =>
            {
                var e = (StartFuturesAdxSignalCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName);
            },
            [typeof(StopFuturesAdxSignalCommand)] = cmd =>
            {
                var e = (StopFuturesAdxSignalCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName);
            },
            [typeof(GenerateFuturesAdxSignalCommand)] = cmd =>
            {
                var e = (GenerateFuturesAdxSignalCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateFuturesAdxSignalId(e.FuturesAdxSignalId, e.CommandName)
                    .ValidateClosedObservation(e.Observation, e.EntityId.ContractId,
                        e.EntityId.ValueDate, e.EntityId.TimePeriod, e.CommandName, allowPriorValueDate: e.IsHistoricalSeed);
            },
            [typeof(GenerateFuturesAdxDailySignalCommand)] = cmd =>
            {
                var e = (GenerateFuturesAdxDailySignalCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateFuturesAdxSignalId(e.FuturesAdxSignalId, e.CommandName);
            }
        };

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <param name="context">The context of the command actor.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="cmd">The command for which state is being loaded. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        return await (_repo.LoadStateAsync(command)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles exceptions that occur during command execution and returns a failed service result containing error
    /// event information.
    /// </summary>
    /// <param name="context">The command actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was encountered.</param>
    /// <param name="command">The command that encountered the exception.</param>
    /// <param name="ex">The exception that was thrown during command processing.</param>
    /// <returns>A failed service result containing a GUID result and error event details describing the failure.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _repo.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously saves the current state of the futures ADX signal actor in response to a command.
    /// </summary>
    /// <param name="context">The context for the actor command execution. Cannot be null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="FuturesAdxSignalCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);
        var adxSignalState = IsArgumentNull.Set((state as FuturesAdxSignalCommandState)!);
        await (_repo.SaveStateAsync(context, adxSignalState, command)).ConfigureAwait(false);
    }

    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => await _repo.SaveStateAsync(context, (FuturesAdxSignalCommandState)state, command, cancellationToken).ConfigureAwait(false);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<FuturesAdxSignalCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            var cmdErrorEvent = await (ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context, command, ActorEntityId.Default, ActorName, global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent.CommandFail)).ConfigureAwait(false);
            return new ServiceFailed<GuidResult>(cmdErrorEvent);
        }
        catch (Exception innerEx)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write6(Context.Logger, innerEx, ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await (ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context, command, ActorEntityId.Default, ActorName, global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent.CommandFail)).ConfigureAwait(false);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx, command);
            }
        }
    }
}
