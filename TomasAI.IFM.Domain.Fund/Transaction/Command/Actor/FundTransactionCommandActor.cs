using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Validation;
using TomasAI.IFM.Domain.Fund.Transaction.Command.State;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;

/// <summary>
/// Represents a command actor responsible for processing fund-related commands and managing the state of a fund within
/// the event-sourced actor system.
/// </summary>
/// <remarks>This actor handles commands such as adding, changing, or removing futures contracts associated with a
/// fund. It coordinates command validation, state loading and saving, and command execution in a thread-safe,
/// event-sourced manner. The actor is typically resolved and managed by the actor system infrastructure.</remarks>
/// <param name="dbEventSource">The event source database context used for logging and persisting command events.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class FundTransactionCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<FundTransactionCommandActor> logger)
    : BaseEventSourceCommandActor<FundTransactionCommandActor>(logger, new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "FundTransactionCommand";
    IEventSourceActorDbContext _dbEventSource = IsArgumentNull.Set(dbEventSource);
    IEventSourceActorStateRepository<FundTransactionCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <remarks>This method resolves the required state repository from the dependency container and ensures
    /// that the base class startup logic is executed. Override this method to include additional startup logic specific
    /// to the actor.</remarks>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnStartup(ICommandActorContext context)
    {
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<FundTransactionCommandState>>());
    }

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
    protected override ICommand ParseMessage(ICommandActorContext context, in NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
        if (msgSubject is not { ActorType: ActorType.Command, Name: ActorName }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var parseFunc))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        var command = parseFunc?.Invoke(message);
        IsArgumentNull.Check(command);
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
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, ICommand>> _parseMap = new()
    {
        [CreateFundTransactionCommand.Verb] = msg => msg.AsCommand<CreateFundTransactionCommand>()!,
        [CreateFundTransactionsCommand.Verb] = msg => msg.AsCommand<CreateFundTransactionsCommand>()!,
        [ProcessEndOfDayFundTransactionCommand.Verb] = msg => msg.AsCommand<ProcessEndOfDayFundTransactionCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of FundTransactionCommandState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains a ServiceResult wrapping a
    /// GuidResult with the command's unique identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        await _dbEventSource.InsertCommandLogAsync(cmd, DateTime.UtcNow, JsonConvert.SerializeObject(cmd));
        var fundTxState = IsArgumentNull.Set((state as FundTransactionCommandState)!);
        var cmdName = cmd.GetType().Name;
        if (! _receiveMap.TryGetValue(cmdName, out var recieveFunc))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {cmd.Subject}");
        return recieveFunc.Invoke(cmd, context, fundTxState);
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding fund transaction command
    /// logic on a given state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of fund transaction-related commands by associating each command
    /// type name with a function that executes the command against a FundTransactionCommandState. The mapping is intended for
    /// internal use to streamline command handling and should not be modified at runtime.</remarks>
    static readonly Dictionary<string, Func<ICommand, ICommandActorContext, FundTransactionCommandState, ServiceResult<GuidResult>>> _receiveMap = new()
    {
        [typeof(CreateFundTransactionCommand).Name] = (cmd, context, state) => (cmd as CreateFundTransactionCommand)!.Execute(state),
        [typeof(CreateFundTransactionsCommand).Name] = (cmd, context, state) => (cmd as CreateFundTransactionsCommand)!.Execute(state),
        [typeof(ProcessEndOfDayFundTransactionCommand).Name] = (cmd, context, state) => (cmd as ProcessEndOfDayFundTransactionCommand)!.Execute(state)
    };

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <remarks>This method performs validation specific to the type of command being processed. It ensures
    /// that the command's identifiers and associated data meet the required criteria. If validation errors are
    /// detected, a <see cref="CommandValidationException"/> is thrown with the relevant error details.</remarks>
    /// <param name="context">The context in which the command is being executed, providing access to services and dependencies.</param>
    /// <param name="threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name="cmd">The command to be validated. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override async ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        var cmdName = cmd.GetType().Name;
        var validationErrors = _validationMap.ContainsKey(cmdName)
            ? _validationMap[cmdName](cmd)
            : throw new InvalidOperationException($"Unable to validate {ActorName} commands from message: {cmd.Subject}");
        validationErrors.ThrowCommandValidationExceptionOnAnyError(cmd.ErrorCode);
    }

    /// <summary>
    /// Provides a mapping from command type names to their corresponding validation functions.
    /// </summary>
    /// <remarks>Each entry associates the name of a command type with a function that performs validation on
    /// instances of that command, returning a list of validation errors. This map enables dynamic selection of
    /// validation logic based on the command type at runtime.</remarks>
    static readonly Dictionary<string, Func<ICommand, List<ValidationError>>> _validationMap = new()
    {
        [typeof(CreateFundTransactionCommand).Name] = cmd => {
            var e = cmd as CreateFundTransactionCommand; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFundTransaction(e.FundTransaction);
        },
        [typeof(CreateFundTransactionsCommand).Name] = cmd => {
            var e = cmd as CreateFundTransactionsCommand; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFundTransactions(e.FundTransactions);
        },
        [typeof(ProcessEndOfDayFundTransactionCommand).Name] = cmd => {
            var e = cmd as ProcessEndOfDayFundTransactionCommand; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFundTransaction(e.FundTransaction);
        }
    };

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <remarks>This method overrides the base implementation to load the actor's state from the repository
    /// using the current command.</remarks>
    /// <param name="context">The context of the command actor, providing information about the current command execution.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="cmd">The command for which state is being loaded. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The task result contains the
    /// loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        return await _repo.LoadStateAsync(cmd);
    }

    /// <summary>
    /// Asynchronously saves the current state of the fund transaction actor in response to a command.
    /// </summary>
    /// <remarks>This method overrides the base implementation to persist the state specific to fund transaction
    /// actors. The state must be of type <see cref="FundTransactionCommandState"/>; otherwise, an exception will be
    /// thrown. All parameters are required and must not be null.</remarks>
    /// <param name="context">The context for the actor command execution, providing access to actor metadata and runtime services. Cannot be
    /// null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="FundTransactionCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var fundTxState = IsArgumentNull.Set((state as FundTransactionCommandState)!);
        await _repo.SaveStateAsync(context, fundTxState, cmd);
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
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            IErrorEvent<FundTransactionEntityId> errorEvent = ex switch
            {
                CreateFundTransactionException
                    => await ex.SendErrorEventAsync<FundTransactionCreatedFailEvent, FundTransactionEntityId>(context, (command as CreateFundTransactionCommand)!, FundTransactionEvent.Actor, FundTransactionEvent.Verb),
                CreateFundTransactionsException
                    => await ex.SendErrorEventAsync<FundTransactionsFailEvent, FundTransactionEntityId>(context, (command as CreateFundTransactionsCommand)!, FundTransactionsEvent.Actor, FundTransactionsEvent.Verb),
                ProcessEndOfDayFundTransactionException
                    => await ex.SendErrorEventAsync<EndOfDayFundTransactionProcessedFailEvent, FundTransactionEntityId>(context, (command as ProcessEndOfDayFundTransactionCommand)!, EndOfDayFundTransactionProcessedEvent.Actor, EndOfDayFundTransactionProcessedEvent.Verb),
                _ => default!
            };
            if (errorEvent is null)
            {
                var cmdErrorEvent = await ex.SendErrorEventAsync<IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            return CommandFailed(ex, command);
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await ex.SendErrorEventAsync<IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx);
            }
        }
    }
}
