using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;

/// <summary>
/// Represents an actor responsible for managing futures option contract commands and their associated state.
/// </summary>
/// <remarks>This actor is designed to handle commands related to futures option contracts, including adding,
/// changing, and removing contracts. It provides mechanisms for parsing messages, validating commands, processing
/// commands, and managing actor state. The actor relies on an <see cref="IEventSourceActorStateRepository{T}"/> for
/// state persistence and interacts with the actor context to execute commands and manage dependencies.</remarks>
/// <param name="logger"></param>
public class FuturesOptionContractCommandActor(
    ICommandActorContext<FuturesOptionContractCommandActor> actorContext,
    IEventProjector<FuturesOptionContractCommandActor> eventProjector)
    : BaseEventSourceCommandActor<FuturesOptionContractCommandActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesOptionContractCommand";
    readonly IEventProjector<FuturesOptionContractCommandActor> _eventProjector = IsArgumentNull.Set(eventProjector);
    IEventSourceActorStateRepository<FuturesOptionContractCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <remarks>This method resolves the required state repository from the dependency container and ensures
    /// it is not null. It also invokes the base class's startup logic to complete the initialization process.</remarks>
    /// <param name="context">The context for the actor, providing access to the dependency container and other runtime services.</param>
    /// <returns></returns>
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesOptionContractCommandActor> context)
    {
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<FuturesOptionContractCommandState>>());
        await _eventProjector.StartAsync(context).ConfigureAwait(false);
    }

    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesOptionContractCommandActor> context)
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
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesOptionContractCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from command verb strings to delegate functions that parse a NATS message into the
    /// corresponding command instance.
    /// </summary>
    /// <remarks>This dictionary enables efficient dispatching and parsing of incoming NATS messages based on
    /// their verb. Each entry associates a specific command verb with a function that converts a NATS message payload
    /// into a strongly typed command object implementing the ICommand interface. The mapping is intended for internal
    /// use in command deserialization and routing scenarios.</remarks>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [AddFuturesOptionContractCommand.Verb] = msg => msg.AsCommand<AddFuturesOptionContractCommand>()!,
        [AddFuturesOptionContractsCommand.Verb] = msg => msg.AsCommand<AddFuturesOptionContractsCommand>()!,
        [ChangeFuturesOptionContractCommand.Verb] = msg => msg.AsCommand<ChangeFuturesOptionContractCommand>()!,
        [RemoveFuturesOptionContractCommand.Verb] = msg => msg.AsCommand<RemoveFuturesOptionContractCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of FuturesOptionContractState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains a ServiceResult wrapping a
    /// GuidResult with the command's unique identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresOptionContractState = IsArgumentNull.Set((state as FuturesOptionContractCommandState)!);
        var receiveFunc = ResolveMappedCommandHandler(cmd, _receiveMap);
        return ValueTask.FromResult(receiveFunc.Invoke(cmd, context, futuresOptionContractState));
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding futures option contract command
    /// logic on a given state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of futures option contract-related commands by associating each command
    /// type name with a function that executes the command against a FuturesOptionContractState. The mapping is intended for
    /// internal use to streamline command handling and should not be modified at runtime.</remarks>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext, FuturesOptionContractCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext, FuturesOptionContractCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(AddFuturesOptionContractCommand)] = (cmd, context, state) => (cmd as AddFuturesOptionContractCommand)!.Execute(state),
        [typeof(AddFuturesOptionContractsCommand)] = (cmd, context, state) => (cmd as AddFuturesOptionContractsCommand)!.Execute(state),
        [typeof(ChangeFuturesOptionContractCommand)] = (cmd, context, state) => (cmd as ChangeFuturesOptionContractCommand)!.Execute(state),
        [typeof(RemoveFuturesOptionContractCommand)] = (cmd, context, state) => (cmd as RemoveFuturesOptionContractCommand)!.Execute(state)
    };

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <remarks>This method performs validation specific to the type of command being processed. It ensures
    /// that the command's identifiers and associated data meet the required criteria. If validation errors are
    /// detected, a <see cref="CommandValidationException"/> is thrown with the relevant error details.</remarks>
    /// <param name="context">The context in which the command is being executed, providing access to services and dependencies.</param>
    /// <param name="threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name="cmd">The command being validated.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, ActorThreadId threadId, ICommand cmd)
        => OnValidateAsync(context, threadId, cmd, CancellationToken.None);

    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, ActorThreadId threadId, ICommand cmd, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        ValidateMappedCommand(cmd, _validationMap);

        var refLookupService = Context.ReferenceLookupService;
        await refLookupService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        ValidateReferenceData(cmd, refLookupService)
            .ThrowCommandValidationExceptionOnAnyError(cmd.ErrorCode);
    }

    /// <summary>
    /// Provides a mapping from command type names to their corresponding validation functions.
    /// </summary>
    /// <remarks>Each entry associates the name of a command type with a function that performs validation on
    /// instances of that command, returning a list of validation errors. This map enables dynamic selection of
    /// validation logic based on the command type at runtime.</remarks>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(AddFuturesOptionContractCommand)] = static cmd => {
            var e = (AddFuturesOptionContractCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateEntityId(e.EntityId, e.CommandName);
        },
        [typeof(AddFuturesOptionContractsCommand)] = static cmd => {
            var e = (AddFuturesOptionContractsCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateEntityId(e.EntityId, e.CommandName);
        },
        [typeof(ChangeFuturesOptionContractCommand)] = static cmd => {
            var e = (ChangeFuturesOptionContractCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateEntityId(e.EntityId, e.CommandName)
                .ValidateFuturesOptionContractId(e.ContractId)
                ;
        },
        [typeof(RemoveFuturesOptionContractCommand)] = static cmd => {
            var e = (RemoveFuturesOptionContractCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateEntityId(e.EntityId, e.CommandName)
                .ValidateFuturesOptionContractId(e.ContractId);
        }
    };

    static List<ValidationError> ValidateReferenceData(
        ICommand command,
        IReferenceLookupService referenceLookupService)
        => command switch
        {
            AddFuturesOptionContractCommand add => new List<ValidationError>()
                .ValidateFuturesOptionContract(add.Contract, referenceLookupService),
            AddFuturesOptionContractsCommand addMany => new List<ValidationError>()
                .ValidateFuturesOptionContracts(addMany.Contracts, referenceLookupService),
            ChangeFuturesOptionContractCommand change => new List<ValidationError>()
                .ValidateFuturesOptionContract(change.Contract, referenceLookupService),
            RemoveFuturesOptionContractCommand => [],
            _ => throw new InvalidOperationException(
                $"Unable to validate {ActorName} reference data for command: {command.Subject}")
        };

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <remarks>This method overrides the base implementation to load the actor's state from the repository
    /// using the current command.</remarks>
    /// <param name="context">The context of the command actor, providing information about the current command execution.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="cmd">The command for which state is being loaded.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The task result contains the
    /// loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        return await _repo.LoadStateAsync(cmd);
    }

    /// <summary>
    /// Asynchronously saves the current state of the futures option contract actor in response to a command.
    /// </summary>
    /// <remarks>This method overrides the base implementation to persist the state specific to futures option
    /// contract actors. The state must be of type <see cref="FuturesOptionContractCommandState"/>; otherwise, an exception will be
    /// thrown. All parameters are required and must not be null.</remarks>
    /// <param name="context">The context for the actor command execution, providing access to actor metadata and runtime services. Cannot be
    /// null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="FuturesOptionContractCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresOptionContractState = IsArgumentNull.Set((state as FuturesOptionContractCommandState)!);
        return _repo.SaveStateAsync(context, futuresOptionContractState, cmd);
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
    /// <param name="command">The command that encountered the exception.</param>
    /// <param name="ex">The exception that was thrown during command processing. Determines the type of error event to generate.</param>
    /// <returns>A failed service result containing a GUID result and error event details describing the failure. The result
    /// reflects the nature of the exception and the associated command context.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, ActorThreadId threadId, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.LoadStateAsync(cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, ActorThreadId threadId, IActorState state, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.SaveStateAsync(context, (FuturesOptionContractCommandState)state, cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<FuturesOptionContractCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            IErrorEvent<FuturesOptionContractEntityId> errorEvent = ex switch
            {
                AddFuturesOptionContractException
                        => await ex.SendErrorEventAsync<FuturesOptionContractAddedFailEvent, FuturesOptionContractEntityId>(context, (command as AddFuturesOptionContractCommand)!, FuturesOptionContractAddedEvent.Actor, FuturesOptionContractAddedEvent.Verb),
                ChangeFuturesOptionContractException
                    => await ex.SendErrorEventAsync<FuturesOptionContractChangedFailEvent, FuturesOptionContractEntityId>(context, (command as ChangeFuturesOptionContractCommand)!, FuturesOptionContractChangedEvent.Actor, FuturesOptionContractChangedEvent.Verb),
                RemoveFuturesOptionContractException
                    => await ex.SendErrorEventAsync<FuturesOptionContractRemovedFailEvent, FuturesOptionContractEntityId>(context, (command as RemoveFuturesOptionContractCommand)!, FuturesOptionContractRemovedEvent.Actor, FuturesOptionContractRemovedEvent.Verb),
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
            Logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
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
