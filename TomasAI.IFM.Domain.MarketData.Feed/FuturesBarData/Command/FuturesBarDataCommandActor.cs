using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;

/// <summary>
/// Represents an actor responsible for managing futures bar data commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesBarDataCommandActor"/> is a specialized command actor designed to handle operations
/// related to futures bar data. It processes commands such as inserting, deleting, starting streaming, and stopping streaming
/// of futures bar data. It validates the commands, and manages the actor's state. This actor relies on an event-sourced
/// repository for state persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="dbEventSource">The event source database context used for logging and persisting command events.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class FuturesBarDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<FuturesBarDataCommandActor> logger)
    : BaseEventSourceCommandActor<FuturesBarDataCommandActor>(logger, new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "FuturesBarDataCommand";
    IEventSourceActorDbContext _dbEventSource = IsArgumentNull.Set(dbEventSource);
    IEventSourceActorStateRepository<FuturesBarDataCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnStartup(ICommandActorContext context)
    {
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>());
    }

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a command instance for the specified actor context.
    /// </summary>
    /// <param name="context">The actor context used to resolve and process the command. Cannot be null.</param>
    /// <param name="message">The NATS message containing the command data to be parsed.</param>
    /// <returns>An <see cref="ICommand"/> instance representing the parsed command from the message.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known command for the actor.</exception>
    protected override ICommand ParseMessage(ICommandActorContext context, in NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
        if (msgSubject is not { ActorType: ActorType.Command, Name: ActorName }
            || !_parseMap.ContainsKey(msgSubject.Verb))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        var command = _parseMap[msgSubject.Verb](message);
        IsArgumentNull.Check(command);
        _dbEventSource.InsertCommandLogAsync(command, DateTime.UtcNow, JsonConvert.SerializeObject(command)).GetAwaiter().GetResult();
        return command;
    }

    /// <summary>
    /// Provides a mapping from command verb strings to delegate functions that parse a NATS message into the
    /// corresponding command instance.
    /// </summary>
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, ICommand>> _parseMap = new()
    {
        [InsertFuturesBarDataCommand.Verb] = msg => msg.AsCommand<InsertFuturesBarDataCommand>()!,
        [DeleteFuturesBarDataCommand.Verb] = msg => msg.AsCommand<DeleteFuturesBarDataCommand>()!,
        [StartFuturesBarDataStreamingCommand.Verb] = msg => msg.AsCommand<StartFuturesBarDataStreamingCommand>()!,
        [StopFuturesBarDataStreamingCommand.Verb] = msg => msg.AsCommand<StopFuturesBarDataStreamingCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of FuturesBarDataCommandState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresBarDataState = IsArgumentNull.Set((state as FuturesBarDataCommandState)!);
        var cmdName = cmd.GetType().Name;
        _ = _receiveMap.ContainsKey(cmdName)
            ? _receiveMap[cmdName](cmd, context, futuresBarDataState)
            : throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {cmd.Subject}");
        return await ValueTask.FromResult(new ServiceOk<GuidResult>(new GuidResult(cmd.CommandId)));
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding futures bar data
    /// command logic on a given state.
    /// </summary>
    static readonly Dictionary<string, Func<ICommand, ICommandActorContext, FuturesBarDataCommandState, bool>> _receiveMap = new()
    {
        [typeof(InsertFuturesBarDataCommand).Name] = (cmd, context, state) => (cmd as InsertFuturesBarDataCommand)!.Execute(state),
        [typeof(DeleteFuturesBarDataCommand).Name] = (cmd, context, state) => (cmd as DeleteFuturesBarDataCommand)!.Execute(state),
        [typeof(StartFuturesBarDataStreamingCommand).Name] = (cmd, context, state) => (cmd as StartFuturesBarDataStreamingCommand)!.Execute(state),
        [typeof(StopFuturesBarDataStreamingCommand).Name] = (cmd, context, state) => (cmd as StopFuturesBarDataStreamingCommand)!.Execute(state)
    };

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <param name="context">The context in which the command is being executed.</param>
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
    static readonly Dictionary<string, Func<ICommand, List<ValidationError>>> _validationMap = new()
    {
        [typeof(InsertFuturesBarDataCommand).Name] = cmd => {
            var e = (InsertFuturesBarDataCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFuturesBarData(e.FuturesBarData);
        },
        [typeof(DeleteFuturesBarDataCommand).Name] = cmd => {
            var e = (DeleteFuturesBarDataCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFuturesBarDataId(e.Id);
        },
        [typeof(StartFuturesBarDataStreamingCommand).Name] = cmd => {
            var e = (StartFuturesBarDataStreamingCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateFuturesContracts(e.Contracts, e.CommandName)
                .ValidateValueDate(e.ValueDate, e.CommandName);
        },
        [typeof(StopFuturesBarDataStreamingCommand).Name] = cmd => {
            var e = (StopFuturesBarDataStreamingCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateValueDate(e.ValueDate, e.CommandName);
        }
    };

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <param name="context">The context of the command actor.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="cmd">The command for which state is being loaded. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        return await _repo.LoadStateAsync(cmd);
    }

    /// <summary>
    /// Asynchronously saves the current state of the futures bar data actor in response to a command.
    /// </summary>
    /// <param name="context">The context for the actor command execution. Cannot be null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="FuturesBarDataCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresBarDataState = IsArgumentNull.Set((state as FuturesBarDataCommandState)!);
        await _repo.SaveStateAsync(context, futuresBarDataState, cmd);
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
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            var cmdErrorEvent = await ex.SendErrorEventAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context, command, ActorEntityId.Default, ActorName, Shared.EventModelActor.Events.CommandExceptionEvent.CommandFail);
            return new ServiceFailed<GuidResult>(cmdErrorEvent);
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await ex.SendErrorEventAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context, command, ActorEntityId.Default, ActorName, Shared.EventModelActor.Events.CommandExceptionEvent.CommandFail);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx, command);
            }
        }
    }
}
