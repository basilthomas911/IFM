using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.Validation;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Actor;

/// <summary>
/// Represents an actor responsible for managing futures contract commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesContractCommandActor"/> is a specialized command actor designed to handle operations
/// related to futures contracts. It processes commands such as adding, changing, and removing futures contracts,
/// validates the commands, and manages the actor's state. This actor relies on an event-sourced repository for state
/// persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="actorContext">The typed futures-contract command context.</param>
/// <param name="eventProjector">The event projector whose lifetime follows this actor.</param>
public class FuturesContractCommandActor(
    ICommandActorContext<FuturesContractCommandActor> actorContext,
    IEventProjector<FuturesContractCommandActor> eventProjector)
    : BaseEventSourceCommandActor<FuturesContractCommandActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesContractCommand";
    readonly ILogger<FuturesContractCommandActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly CommandAuditTracker _commandAudit = new(IsArgumentNull.Set(actorContext.DbEventSource));
    readonly IEventProjector<FuturesContractCommandActor> _eventProjector = IsArgumentNull.Set(eventProjector);
    IEventSourceActorStateRepository<FuturesContractCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <remarks>This method resolves the required state repository from the dependency container and ensures
    /// that the base class startup logic is executed. Override this method to include additional startup logic specific
    /// to the actor.</remarks>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesContractCommandActor> context)
    {
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<FuturesContractCommandState>>());
        await _eventProjector.StartAsync(context).ConfigureAwait(false);
    }

    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesContractCommandActor> context)
        => await _eventProjector.StopAsync().ConfigureAwait(false);

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a command instance for the specified actor context.
    /// </summary>
    /// <remarks>The parsed command is synchronously logged to the database before being returned. This method
    /// expects the message subject to match a registered command verb for the actor.</remarks>
    /// <param name="context">The actor context used to resolve and process the command. Cannot be null.</param>
    /// <param name="message">The NATS message containing the command data to be parsed. Must have a subject and payload appropriate for
    /// command resolution.</param>
    /// <returns>An <see cref="ICommand"/> instance representing the parsed command from the message.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known command for the actor, or if command resolution
    /// fails.</exception>
    protected override ICommand ParseMessage(ICommandActorContext<FuturesContractCommandActor> context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Command, Name: ActorName }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var messageParser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        var command = messageParser.Invoke(message);
        IsArgumentNull.Check(command);
        _commandAudit.Start(command);
        return command;
    }

    /// <summary>
    /// Provides a mapping from command verb strings to delegate functions that parse a NATS message into the
    /// corresponding command instance.
    /// </summary>
    /// <remarks>This dictionary enables efficient dispatching and parsing of incoming NATS messages based on
    /// their verb. Each entry associates a specific command verb with a function that converts a NATS message payload
    /// into a strongly typed command object implementing the ICommand interface. The mapping is intended for internal
    /// use in command deserialization and routing scenarios.</remarks>
    static readonly Dictionary<string, Func<IActorMessage, ICommand>> _parseMap = new()
    {
        [AddFuturesContractCommand.Verb] = msg => msg.AsCommand<AddFuturesContractCommand>()!,
        [ChangeFuturesContractCommand.Verb] = msg => msg.AsCommand<ChangeFuturesContractCommand>()!,
        [RemoveFuturesContractCommand.Verb] = msg => msg.AsCommand<RemoveFuturesContractCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of FuturesContractState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains a ServiceResult wrapping a
    /// GuidResult with the command's unique identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesContractCommandActor> context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresContractState = IsArgumentNull.Set((state as FuturesContractCommandState)!);
        var cmdName = cmd.GetType().Name;
        if (!_receiveMap.TryGetValue(cmdName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {cmd.Subject}");
        _ = receiveFunc.Invoke(cmd, context, futuresContractState);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(cmd.CommandId)));
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding futures contract command
    /// logic on a given state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of futures contract-related commands by associating each command
    /// type name with a function that executes the command against a FuturesContractState. The mapping is intended for
    /// internal use to streamline command handling and should not be modified at runtime.</remarks>
    static readonly Dictionary<string, Func<ICommand, ICommandActorContext, FuturesContractCommandState, bool>> _receiveMap = new()
    {
        [typeof(AddFuturesContractCommand).Name] = (cmd, context, state) => (cmd as AddFuturesContractCommand)!.Execute(state),
        [typeof(ChangeFuturesContractCommand).Name] = (cmd, context, state) => (cmd as ChangeFuturesContractCommand)!.Execute(state),
        [typeof(RemoveFuturesContractCommand).Name] = (cmd, context, state) => (cmd as RemoveFuturesContractCommand)!.Execute(state)
    };

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <remarks>This method performs validation specific to the type of command being processed. It ensures
    /// that the command's identifiers and associated data meet the required criteria. If validation errors are
    /// detected, a <see cref="CommandValidationException"/> is thrown with the relevant error details.</remarks>
    /// <param name="context">The context in which the command is being executed, providing access to services and dependencies.</param>
    /// <param name="threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name="verb">The verb associated with the command being validated.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, ICommand cmd)
        => OnValidateAsync(context, threadId, cmd, CancellationToken.None);

    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, ICommand cmd, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        await _commandAudit.CompleteAsync(cmd, cancellationToken).ConfigureAwait(false);
        var refLookupService = actorContext.ReferenceLookupService;
        await refLookupService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var cmdName = cmd.GetType().Name;
        if (!_validationMap.TryGetValue(cmdName, out var getValidationErrors))
            throw new InvalidOperationException($"Unable to validate {ActorName} commands from message: {cmd.Subject}");
        getValidationErrors
            .Invoke(cmd, refLookupService)
            .ThrowCommandValidationExceptionOnAnyError(cmd.ErrorCode);
    }

    /// <summary>
    /// Provides a mapping from command type names to their corresponding validation functions.
    /// </summary>
    /// <remarks>Each entry associates the name of a command type with a function that performs validation on
    /// instances of that command, returning a list of validation errors. This map enables dynamic selection of
    /// validation logic based on the command type at runtime.</remarks>
    static readonly Dictionary<string, Func<ICommand, IReferenceLookupService, List<ValidationError>>> _validationMap = new()
    {
        [typeof(AddFuturesContractCommand).Name] = (cmd, refService) => {
            var e = (AddFuturesContractCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFuturesContract(e.Contract, refService);
        },
        [typeof(ChangeFuturesContractCommand).Name] = (cmd, refService) => {
            var e = (ChangeFuturesContractCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFuturesContractEntityId(e.ContractId)
                .ValidateFuturesContract(e.Contract, refService);
        },
        [typeof(RemoveFuturesContractCommand).Name] = (cmd, refService) => {
            var e = (RemoveFuturesContractCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFuturesContractEntityId(e.ContractId);
        }
    };

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <remarks>This method overrides the base implementation to load the actor's state from the repository
    /// using the current command.</remarks>
    /// <param name="context">The context of the command actor, providing information about the current command execution.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="verb">The verb associated with the command for which state is being loaded.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The task result contains the
    /// loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd); 
        return await _repo.LoadStateAsync(cmd);
    }

    /// <summary>
    /// Asynchronously saves the current state of the futures contract actor in response to a command.
    /// </summary>
    /// <remarks>This method overrides the base implementation to persist the state specific to futures
    /// contract actors. The state must be of type <see cref="FuturesContractCommandState"/>; otherwise, an exception will be
    /// thrown. All parameters are required and must not be null.</remarks>
    /// <param name="context">The context for the actor command execution, providing access to actor metadata and runtime services. Cannot be
    /// null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="FuturesContractCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresContractState =  IsArgumentNull.Set((state as FuturesContractCommandState)!);
        return _repo.SaveStateAsync(context, futuresContractState, cmd);
    }

    /// <summary>
    /// Handles exceptions that occur during command execution and returns a failed service result containing error
    /// event information.
    /// </summary>
    /// <remarks>This method maps specific command-related exceptions to corresponding error events and
    /// ensures that all exceptions are reported as failed service results. If error event generation fails, a generic
    /// command exception event is returned. The method is asynchronous and may log additional errors if exception
    /// handling itself fails.</remarks>
    /// <param name="context">The command actor context in which the exception occurred. Provides access to message and command details
    /// relevant to error handling.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was encountered. Used to correlate error events with the
    /// specific execution thread.</param>
    /// <param name="verb">The verb associated with the command that encountered the exception.</param>
    /// <param name="ex">The exception that was thrown during command processing. Determines the type of error event to generate.</param>
    /// <returns>A failed service result containing a GUID result and error event details describing the failure. The result
    /// reflects the nature of the exception and the associated command context.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.LoadStateAsync(cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, IActorState state, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.SaveStateAsync(context, (FuturesContractCommandState)state, cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            IErrorEvent<FuturesContractId> errorEvent = ex switch
            {
                AddFuturesContractException
                        => await ex.SendErrorEventAsync<FuturesContractAddedFailEvent, FuturesContractId>(context, (command as AddFuturesContractCommand)!, FuturesContractAddedEvent.Actor, FuturesContractAddedEvent.Verb),
                ChangeFuturesContractException
                    => await ex.SendErrorEventAsync<FuturesContractChangedFailEvent, FuturesContractId>(context, (command as ChangeFuturesContractCommand)!, FuturesContractChangedEvent.Actor, FuturesContractChangedEvent.Verb),
                RemoveFuturesContractException
                    => await ex.SendErrorEventAsync<FuturesContractRemovedFailEvent, FuturesContractId>(context, (command as RemoveFuturesContractCommand)!, FuturesContractRemovedEvent.Actor, FuturesContractRemovedEvent.Verb),
                _ => default!,
            };
            if (errorEvent is null)
            {
                var cmdErrorEvent = await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            return CommandFailed(ex, command);
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx, command);
            }
        }

    }
}

