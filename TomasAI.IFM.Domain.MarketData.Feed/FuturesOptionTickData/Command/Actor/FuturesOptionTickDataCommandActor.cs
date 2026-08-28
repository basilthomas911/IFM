using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using global::TomasAI.IFM.Shared.EventModelActor;
using global::TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Validation;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;

using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;

/// <summary>
/// Represents an actor responsible for managing futures option tick data commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesOptionTickDataCommandActor"/> is a specialized command actor designed to handle operations
/// related to futures option tick data. It processes commands such as inserting tick data, starting streaming, and stopping streaming
/// of futures option tick data. It validates the commands, and manages the actor's state. This actor relies on an event-sourced
/// repository for state persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="dbEventSource">The event source database context used for logging and persisting command events.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class FuturesOptionTickDataCommandActor(
    ICommandActorContext<FuturesOptionTickDataCommandActor> actorContext,
    IEventProjector<FuturesOptionTickDataCommandActor> eventProjector)
    : BaseEventSourceCommandActor<FuturesOptionTickDataCommandActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesOptionTickDataCommand";
    readonly ILogger<FuturesOptionTickDataCommandActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly IEventProjector<FuturesOptionTickDataCommandActor> _eventProjector = IsArgumentNull.Set(eventProjector);
    IEventSourceActorStateRepository<FuturesOptionTickDataCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnStartup(ICommandActorContext<FuturesOptionTickDataCommandActor> context)
    {
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>());
        await _eventProjector.StartAsync(context).ConfigureAwait(false);
    }
    protected override async ValueTask OnShutdown(ICommandActorContext<FuturesOptionTickDataCommandActor> context)
        => await _eventProjector.StopAsync().ConfigureAwait(false);

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a command instance for the specified actor context.
    /// </summary>
    /// <param name="context">The actor context used to resolve and process the command. Cannot be null.</param>
    /// <param name="message">The NATS message containing the command data to be parsed.</param>
    /// <returns>An <see cref="ICommand"/> instance representing the parsed command from the message.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known command for the actor.</exception>
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesOptionTickDataCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from command verb strings to delegate functions that parse a NATS message into the
    /// corresponding command instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [InsertFuturesOptionTickDataCommand.Verb] = msg => msg.AsCommand<InsertFuturesOptionTickDataCommand>()!,
        [StartFuturesOptionTickDataStreamingCommand.Verb] = msg => msg.AsCommand<StartFuturesOptionTickDataStreamingCommand>()!,
        [StopFuturesOptionTickDataStreamingCommand.Verb] = msg => msg.AsCommand<StopFuturesOptionTickDataStreamingCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of FuturesOptionTickDataCommandState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesOptionTickDataCommandActor> context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresOptionTickDataState = IsArgumentNull.Set((state as FuturesOptionTickDataCommandState)!);
        var receiveFunc = ResolveMappedCommandHandler(cmd, _receiveMap);
        return ValueTask.FromResult(receiveFunc.Invoke(cmd, context, futuresOptionTickDataState));
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding futures option tick data
    /// command logic on a given state.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext,
        FuturesOptionTickDataCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext,
        FuturesOptionTickDataCommandState, ServiceResult<GuidResult>>>()
    {
        [typeof(InsertFuturesOptionTickDataCommand)] = (cmd, context, state) => (cmd as InsertFuturesOptionTickDataCommand)!.Execute(state),
        [typeof(StartFuturesOptionTickDataStreamingCommand)] = (cmd, context, state) => (cmd as StartFuturesOptionTickDataStreamingCommand)!.Execute(state),
        [typeof(StopFuturesOptionTickDataStreamingCommand)] = (cmd, context, state) => (cmd as StopFuturesOptionTickDataStreamingCommand)!.Execute(state)
    };

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <param name="context">The context in which the command is being executed.</param>
    /// <param name="threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name="cmd">The command to be validated. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override async ValueTask OnValidateAsync(ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        ValidateMappedCommand(cmd, _validationMap);

        var refLookupService = context.Container.Resolve<IReferenceLookupService>();
        await refLookupService.EnsureLoadedAsync().ConfigureAwait(false);
        ValidateReferenceData(cmd, refLookupService)
            .ThrowCommandValidationExceptionOnAnyError(cmd.ErrorCode);
    }

    /// <summary>
    /// Provides a mapping from command type names to their corresponding validation functions.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(InsertFuturesOptionTickDataCommand)] = static cmd => {
            var e = (InsertFuturesOptionTickDataCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateEntityId(e.EntityId, e.CommandName)
                .ValidateContract(e.Contract, e.CommandName)
                .ValidateOptionTickData(e.OptionTickData, e.CommandName);
        },
        [typeof(StartFuturesOptionTickDataStreamingCommand)] = static cmd => {
            var e = (StartFuturesOptionTickDataStreamingCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateEntityId(e.EntityId, e.CommandName)
                .ValidateBaseContract(e.BaseContract, e.CommandName)
                .ValidateValueDate(e.ValueDate, e.CommandName)
                .ValidateMaturityDate(e.MaturityDate, e.CommandName)
                .ValidateRiskFreeRate(e.RiskFreeRate, e.CommandName);
        },
        [typeof(StopFuturesOptionTickDataStreamingCommand)] = static cmd => {
            var e = (StopFuturesOptionTickDataStreamingCommand)cmd; return new List<ValidationError>()
                .ValidateCommandId(e.CommandId, e.CommandName)
                .ValidateEntityId(e.EntityId, e.CommandName)
                .ValidateContractId(e.ContractId, e.CommandName);
        }
    };

    static List<ValidationError> ValidateReferenceData(
        ICommand command,
        IReferenceLookupService referenceLookupService)
        => command switch
        {
            StartFuturesOptionTickDataStreamingCommand start => new List<ValidationError>()
                .ValidateFuturesOptionContract(
                    start.Contract,
                    referenceLookupService,
                    start.CommandName),
            InsertFuturesOptionTickDataCommand or StopFuturesOptionTickDataStreamingCommand => [],
            _ => throw new InvalidOperationException(
                $"Unable to validate {ActorName} reference data for command: {command.Subject}")
        };

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <param name="context">The context of the command actor.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="cmd">The command for which state is being loaded. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        return await _repo.LoadStateAsync(cmd);
    }

    /// <summary>
    /// Asynchronously saves the current state of the futures option tick data actor in response to a command.
    /// </summary>
    /// <param name="context">The context for the actor command execution. Cannot be null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="FuturesOptionTickDataCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var futuresOptionTickDataState = IsArgumentNull.Set((state as FuturesOptionTickDataCommandState)!);
        await _repo.SaveStateAsync(context, futuresOptionTickDataState, cmd);
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
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            var cmdErrorEvent = await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context, command, ActorEntityId.Default, ActorName, global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent.CommandFail);
            return new ServiceFailed<GuidResult>(cmdErrorEvent);
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context, command, ActorEntityId.Default, ActorName, global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent.CommandFail);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx, command);
            }
        }
    }
}
