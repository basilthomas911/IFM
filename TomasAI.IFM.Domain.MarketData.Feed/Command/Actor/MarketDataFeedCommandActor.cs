using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using global::TomasAI.IFM.Shared.EventModelActor;
using global::TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;

using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;

/// <summary>
/// Represents an actor responsible for managing market data feed commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="MarketDataFeedCommandActor"/> is a specialized command actor designed to handle operations
/// related to market data feeds. It processes commands such as starting, stopping, and resetting feeds, adding and
/// removing trade live feeds, and deleting streaming request identifiers. It validates the commands, and manages the
/// actor's state. This actor relies on an event-sourced repository for state persistence and uses dependency injection
/// to resolve required services.</remarks>
/// <param name="dbEventSource">The event source database context used for logging and persisting command events.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class MarketDataFeedCommandActor(
    ICommandActorContext<MarketDataFeedCommandActor> actorContext,
    IEventProjector<MarketDataFeedCommandActor> eventProjector)
    : BaseEventSourceCommandActor<MarketDataFeedCommandActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "MarketDataFeedCommand";
    readonly ILogger<MarketDataFeedCommandActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly IEventProjector<MarketDataFeedCommandActor> _eventProjector = IsArgumentNull.Set(eventProjector);
    IEventSourceActorStateRepository<MarketDataFeedCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <remarks>This method resolves the required state repository from the dependency container and ensures
    /// that the base class startup logic is executed. Override this method to include additional startup logic specific
    /// to the actor.</remarks>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnStartup(ICommandActorContext<MarketDataFeedCommandActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<MarketDataFeedCommandState>>());
        await _eventProjector.StartAsync(context).ConfigureAwait(false);
    }
    protected override async ValueTask OnShutdown(ICommandActorContext<MarketDataFeedCommandActor> context)
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
        ICommandActorContext<MarketDataFeedCommandActor> context,
        IActorMessage message)
    {
        var command = ParseMappedCommand(context, message, _parseMap);
        return command is StartMarketDataFeedCommand start
            ? start.ResolveSession(context.Container.Resolve<IFuturesMarketSessionAuthority>().Current,
                context.Container.Resolve<TimeProvider>().GetUtcNow())
            : command;
    }

    /// <summary>
    /// Provides a mapping from command verb strings to delegate functions that parse a NATS message into the
    /// corresponding command instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [StartMarketDataFeedCommand.Verb] = msg => msg.AsCommand<StartMarketDataFeedCommand>()!,
        [StopMarketDataFeedCommand.Verb] = msg => msg.AsCommand<StopMarketDataFeedCommand>()!,
        [ResetMarketDataFeedCommand.Verb] = msg => msg.AsCommand<ResetMarketDataFeedCommand>()!,
        [AddTradeLiveFeedCommand.Verb] = msg => msg.AsCommand<AddTradeLiveFeedCommand>()!,
        [RemoveTradeLiveFeedCommand.Verb] = msg => msg.AsCommand<RemoveTradeLiveFeedCommand>()!,
        [TurnTradeLiveFeedOnCommand.Verb] = msg => msg.AsCommand<TurnTradeLiveFeedOnCommand>()!,
        [TurnTradeLiveFeedOffCommand.Verb] = msg => msg.AsCommand<TurnTradeLiveFeedOffCommand>()!,
        [DeleteStreamingRequestIdCommand.Verb] = msg => msg.AsCommand<DeleteStreamingRequestIdCommand>()!,
        [HaltTradeLiveFeedCommand.Verb] = msg => msg.AsCommand<HaltTradeLiveFeedCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of MarketDataFeedCommandState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains a ServiceResult wrapping a
    /// GuidResult with the command's unique identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<MarketDataFeedCommandActor> context, IActorState state, ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);
        var marketDataFeedState = IsArgumentNull.Set((state as MarketDataFeedCommandState)!);
        var receiveFunc = ResolveMappedCommandHandler(command, _receiveMap);
        return await receiveFunc.Invoke(command, context, marketDataFeedState).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding market data feed command
    /// logic on a given state.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<MarketDataFeedCommandActor>,
        MarketDataFeedCommandState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<MarketDataFeedCommandActor>,
        MarketDataFeedCommandState, ValueTask<ServiceResult<GuidResult>>>>()
        {
            [typeof(StartMarketDataFeedCommand)] = (cmd, context, state) => ExecuteStartAsync((StartMarketDataFeedCommand)cmd, context, state),
            [typeof(StopMarketDataFeedCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as StopMarketDataFeedCommand)!.Execute(state)),
            [typeof(ResetMarketDataFeedCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as ResetMarketDataFeedCommand)!.Execute(state)),
            [typeof(AddTradeLiveFeedCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as AddTradeLiveFeedCommand)!.Execute(state)),
            [typeof(RemoveTradeLiveFeedCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as RemoveTradeLiveFeedCommand)!.Execute(state)),
            [typeof(TurnTradeLiveFeedOnCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as TurnTradeLiveFeedOnCommand)!.Execute(state)),
            [typeof(TurnTradeLiveFeedOffCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as TurnTradeLiveFeedOffCommand)!.Execute(state)),
            [typeof(DeleteStreamingRequestIdCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as DeleteStreamingRequestIdCommand)!.Execute(state)),
            [typeof(HaltTradeLiveFeedCommand)] = (cmd, context, state) => ValueTask.FromResult((cmd as HaltTradeLiveFeedCommand)!.Execute(state))
        };

    /// <summary>Loads the API-prepared assignments without discovering contracts in the caller.</summary>
    internal static async ValueTask<ServiceResult<GuidResult>> ExecuteStartAsync(StartMarketDataFeedCommand start, ICommandActorContext<MarketDataFeedCommandActor> context, MarketDataFeedCommandState state)
    {
        var contracts = await context.Container.Resolve<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>()
            .PrepareSessionAsync(start.ValueDate, CancellationToken.None).ConfigureAwait(false);
        context.Logger.LogInformation("Starting Host-backed market session {ValueDate} with {ContractCount} prepared contracts; CommandId={CommandId}.",
            start.ValueDate, contracts.Length, start.CommandId);
        return start.Execute(state, contracts);
    }

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
    protected override async ValueTask OnValidateAsync(ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, ICommand command)
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
            [typeof(StartMarketDataFeedCommand)] = cmd =>
            {
                var e = (StartMarketDataFeedCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate");
            },
            [typeof(StopMarketDataFeedCommand)] = cmd =>
            {
                var e = (StopMarketDataFeedCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate");
            },
            [typeof(ResetMarketDataFeedCommand)] = cmd =>
            {
                var e = (ResetMarketDataFeedCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateFuturesContracts(e.FuturesContracts)
                    .ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate");
            },
            [typeof(AddTradeLiveFeedCommand)] = cmd =>
            {
                var e = (AddTradeLiveFeedCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateTradeFeedIdentity(e.EntityId, e.Subject, e.CommandName)
                    .ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate");
            },
            [typeof(RemoveTradeLiveFeedCommand)] = cmd =>
            {
                var e = (RemoveTradeLiveFeedCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateTradeFeedIdentity(e.EntityId, e.Subject, e.CommandName)
                    .ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate");
            },
            [typeof(TurnTradeLiveFeedOnCommand)] = cmd =>
            {
                var e = (TurnTradeLiveFeedOnCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateTradeFeedIdentity(e.EntityId, e.Subject, e.CommandName)
                    .ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate");
            },
            [typeof(TurnTradeLiveFeedOffCommand)] = cmd =>
            {
                var e = (TurnTradeLiveFeedOffCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateTradeFeedIdentity(e.EntityId, e.Subject, e.CommandName)
                    .ValidateDateOnly(e.ValueDate, e.CommandName, "ValueDate");
            },
            [typeof(DeleteStreamingRequestIdCommand)] = cmd =>
            {
                var e = (DeleteStreamingRequestIdCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateFeedId(e.FeedId);
            },
            [typeof(HaltTradeLiveFeedCommand)] = cmd =>
            {
                var e = (HaltTradeLiveFeedCommand)cmd; return new List<ValidationError>()
                    .ValidateCommandId(e.CommandId, e.CommandName)
                    .ValidateEntityId(e.EntityId, e.CommandName)
                    .ValidateTradeFeedIdentity(e.EntityId, e.Subject, e.CommandName);
            }
        };

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <param name="context">The context of the command actor, providing information about the current command execution.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="cmd">The command for which state is being loaded. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The task result contains the
    /// loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        return await (_repo.LoadStateAsync(command)).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously saves the current state of the market data feed actor in response to a command.
    /// </summary>
    /// <param name="context">The context for the actor command execution, providing access to actor metadata and runtime services. Cannot be
    /// null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="MarketDataFeedCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);
        var marketDataFeedState = IsArgumentNull.Set((state as MarketDataFeedCommandState)!);
        await (_repo.SaveStateAsync(context, marketDataFeedState, command)).ConfigureAwait(false);
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    /// <summary>
    /// Handles exceptions that occur during command execution and returns a failed service result containing error
    /// event information.
    /// </summary>
    /// <param name="context">The command actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was encountered.</param>
    /// <param name="command">The command that encountered the exception.</param>
    /// <param name="ex">The exception that was thrown during command processing.</param>
    /// <returns>A failed service result containing a GUID result and error event details describing the failure.</returns>
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            IErrorEvent<TradeLiveFeedId> errorEvent = ex switch
            {
                AddTradeLiveFeedException
                    => await (ex.SendErrorEventAsync<TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(
                        ErrorType.Command, context, command, ActorEntityId.Default, TradeLiveFeedAddedFailEvent.Actor, TradeLiveFeedAddedFailEvent.Verb)).ConfigureAwait(false),
                RemoveTradeLiveFeedException
                    => await (ex.SendErrorEventAsync<TradeLiveFeedRemovedFailEvent, TradeLiveFeedId>(
                        ErrorType.Command, context, command, ActorEntityId.Default, TradeLiveFeedRemovedFailEvent.Actor, TradeLiveFeedRemovedFailEvent.Verb)).ConfigureAwait(false),
                TurnTradeLiveFeedOnException
                    => await (ex.SendErrorEventAsync<TradeLiveFeedTurnedOnFailEvent, TradeLiveFeedId>(
                        ErrorType.Command, context, command, ActorEntityId.Default, TradeLiveFeedTurnedOnFailEvent.Actor, TradeLiveFeedTurnedOnFailEvent.Verb)).ConfigureAwait(false),
                TurnTradeLiveFeedOffException
                    => await (ex.SendErrorEventAsync<TradeLiveFeedTurnedOffFailEvent, TradeLiveFeedId>(
                        ErrorType.Command, context, command, ActorEntityId.Default, TradeLiveFeedTurnedOffFailEvent.Actor, TradeLiveFeedTurnedOffFailEvent.Verb)).ConfigureAwait(false),
                _ => default!
            };
            if (errorEvent is null)
            {
                var cmdErrorEvent = await (ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context)).ConfigureAwait(false);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            return CommandFailed(ex);
        }
        catch (Exception innerEx)
        {
            global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write21(_logger, innerEx, ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await (ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context)).ConfigureAwait(false);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx);
            }
        }
    }
}
