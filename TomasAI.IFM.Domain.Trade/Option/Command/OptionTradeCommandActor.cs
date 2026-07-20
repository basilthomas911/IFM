using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.OptionPricer.Validation;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.TradeOrder.Validation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.Validation;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command;

/// <summary>
/// Represents an actor responsible for managing option trade commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="OptionTradeCommandActor"/> is a specialized command actor designed to handle operations
/// related to option trades. It processes commands such as opening, closing, deleting, snapshotting, placing orders,
/// changing leg data, updating distribution statistics, and processing end-of-day operations. It validates the commands,
/// and manages the actor's state. This actor relies on an event-sourced repository for state persistence and uses
/// dependency injection to resolve required services.</remarks>
/// <param name="dbEventSource">The event source database context used for logging and persisting command events.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class OptionTradeCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<OptionTradeCommandActor> logger)
    : BaseEventSourceCommandActor<OptionTradeCommandActor>(logger, new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "OptionTradeCommand";
    IEventSourceActorDbContext _dbEventSource = IsArgumentNull.Set(dbEventSource);
    IEventSourceActorStateRepository<OptionTradeCommandState> _repo = default!;

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
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<OptionTradeCommandState>>());
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
        [PlaceOptionTradeOrderCommand.Verb] = msg => msg.AsCommand<PlaceOptionTradeOrderCommand>()!,
        [OpenOptionTradeCommand.Verb] = msg => msg.AsCommand<OpenOptionTradeCommand>()!,
        [CloseOptionTradeCommand.Verb] = msg => msg.AsCommand<CloseOptionTradeCommand>()!,
        [DeleteOptionTradeCommand.Verb] = msg => msg.AsCommand<DeleteOptionTradeCommand>()!,
        [SnapshotOptionTradeCommand.Verb] = msg => msg.AsCommand<SnapshotOptionTradeCommand>()!,
        [ChangeOptionTradeLegDataCommand.Verb] = msg => msg.AsCommand<ChangeOptionTradeLegDataCommand>()!,
        [UpdateOptionTradeSpreadDistributionStatisticsCommand.Verb] = msg => msg.AsCommand<UpdateOptionTradeSpreadDistributionStatisticsCommand>()!,
        [OpenOptionTradePositionCommand.Verb] = msg => msg.AsCommand<OpenOptionTradePositionCommand>()!,
        [CloseOptionTradePositionCommand.Verb] = msg => msg.AsCommand<CloseOptionTradePositionCommand>()!,
        [DeleteOptionTradeSpreadBarDataCommand.Verb] = msg => msg.AsCommand<DeleteOptionTradeSpreadBarDataCommand>()!,
        [InsertOptionTradeSpreadBarDataCommand.Verb] = msg => msg.AsCommand<InsertOptionTradeSpreadBarDataCommand>()!,
        [InsertOptionTradeSpreadDataCommand.Verb] = msg => msg.AsCommand<InsertOptionTradeSpreadDataCommand>()!,
        [ProcessOptionTradeEndOfDayCommand.Verb] = msg => msg.AsCommand<ProcessOptionTradeEndOfDayCommand>()!,
        [UpdateOptionTradeDailyProfitTargetCommand.Verb] = msg => msg.AsCommand<UpdateOptionTradeDailyProfitTargetCommand>()!
    };

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of OptionTradeCommandState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains a ServiceResult wrapping a
    /// GuidResult with the command's unique identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var optionTradeState = IsArgumentNull.Set((state as OptionTradeCommandState)!);
        var cmdName = cmd.GetType().Name;
        _ = _receiveMap.ContainsKey(cmdName)
            ? _receiveMap[cmdName](cmd, context, optionTradeState)
            : throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {cmd.Subject}");
        return await ValueTask.FromResult(new ServiceOk<GuidResult>(new GuidResult(cmd.CommandId)));
    }

    /// <summary>
    /// Provides a mapping from command type names to delegate functions that execute the corresponding option trade command
    /// logic on a given state.
    /// </summary>
    static readonly Dictionary<string, Func<ICommand, ICommandActorContext, OptionTradeCommandState, bool>> _receiveMap = new()
    {
        [typeof(PlaceOptionTradeOrderCommand).Name] = (cmd, context, state) => (cmd as PlaceOptionTradeOrderCommand)!.Execute(state),
        [typeof(OpenOptionTradeCommand).Name] = (cmd, context, state) => (cmd as OpenOptionTradeCommand)!.Execute(state),
        [typeof(CloseOptionTradeCommand).Name] = (cmd, context, state) => (cmd as CloseOptionTradeCommand)!.Execute(state),
        [typeof(DeleteOptionTradeCommand).Name] = (cmd, context, state) => (cmd as DeleteOptionTradeCommand)!.Execute(state),
        [typeof(SnapshotOptionTradeCommand).Name] = (cmd, context, state) => (cmd as SnapshotOptionTradeCommand)!.Execute(state),
        [typeof(ChangeOptionTradeLegDataCommand).Name] = (cmd, context, state) => (cmd as ChangeOptionTradeLegDataCommand)!.Execute(state),
        [typeof(UpdateOptionTradeSpreadDistributionStatisticsCommand).Name] = (cmd, context, state) => (cmd as UpdateOptionTradeSpreadDistributionStatisticsCommand)!.Execute(state),
        [typeof(OpenOptionTradePositionCommand).Name] = (cmd, context, state) => (cmd as OpenOptionTradePositionCommand)!.Execute(state),
        [typeof(CloseOptionTradePositionCommand).Name] = (cmd, context, state) => (cmd as CloseOptionTradePositionCommand)!.Execute(state),
        [typeof(DeleteOptionTradeSpreadBarDataCommand).Name] = (cmd, context, state) => (cmd as DeleteOptionTradeSpreadBarDataCommand)!.Execute(state),
        [typeof(InsertOptionTradeSpreadBarDataCommand).Name] = (cmd, context, state) => (cmd as InsertOptionTradeSpreadBarDataCommand)!.Execute(state),
        [typeof(InsertOptionTradeSpreadDataCommand).Name] = (cmd, context, state) => (cmd as InsertOptionTradeSpreadDataCommand)!.Execute(state),
        [typeof(ProcessOptionTradeEndOfDayCommand).Name] = (cmd, context, state) => (cmd as ProcessOptionTradeEndOfDayCommand)!.Execute(state),
        [typeof(UpdateOptionTradeDailyProfitTargetCommand).Name] = (cmd, context, state) => (cmd as UpdateOptionTradeDailyProfitTargetCommand)!.Execute(state)
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
    static readonly Dictionary<string, Func<ICommand, List<ValidationError>>> _validationMap = new()
    {
        [typeof(PlaceOptionTradeOrderCommand).Name] = cmd => {
            var e = cmd as PlaceOptionTradeOrderCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName)
                .ValidateTradeOrder(e.TradeOrder);
        },
        [typeof(OpenOptionTradeCommand).Name] = cmd => {
            var e = cmd as OpenOptionTradeCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName)
                .ValidateTradeOrder(e.TradeOrder);
        },
        [typeof(CloseOptionTradeCommand).Name] = cmd => {
            var e = cmd as CloseOptionTradeCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName)
                .ValidateTradeOrder(e.TradeOrder);
        },
        [typeof(DeleteOptionTradeCommand).Name] = cmd => {
            var e = cmd as DeleteOptionTradeCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName);
        },
        [typeof(SnapshotOptionTradeCommand).Name] = cmd => {
            var e = cmd as SnapshotOptionTradeCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName);
        },
        [typeof(ChangeOptionTradeLegDataCommand).Name] = cmd => {
            var e = cmd as ChangeOptionTradeLegDataCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateParameters(e);
        },
        [typeof(UpdateOptionTradeSpreadDistributionStatisticsCommand).Name] = cmd => {
            var e = cmd as UpdateOptionTradeSpreadDistributionStatisticsCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName)
                .ValidateValueDate(e.ValueDate, e.CommandName)
                .ValidateDaysToExpiry(e.DaysToExpiry, e.CommandName)
                .ValidateSpreadDistribution(e.PutSpreadDistribution, e.CallSpreadDistribution, e.CommandName);
        },
        [typeof(OpenOptionTradePositionCommand).Name] = cmd => {
            var e = cmd as OpenOptionTradePositionCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName);
        },
        [typeof(CloseOptionTradePositionCommand).Name] = cmd => {
            var e = cmd as CloseOptionTradePositionCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName);
        },
        [typeof(DeleteOptionTradeSpreadBarDataCommand).Name] = cmd => {
            var e = cmd as DeleteOptionTradeSpreadBarDataCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName)
                .ValidateValueDate(e.ValueDate, e.CommandName);
        },
        [typeof(InsertOptionTradeSpreadBarDataCommand).Name] = cmd => {
            var e = cmd as InsertOptionTradeSpreadBarDataCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeSpreadBarData(e.OptionTradeSpreadBarData);
        },
        [typeof(InsertOptionTradeSpreadDataCommand).Name] = cmd => {
            var e = cmd as InsertOptionTradeSpreadDataCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeSpreadData(e.OptionTradeSpreadData);
        },
        [typeof(ProcessOptionTradeEndOfDayCommand).Name] = cmd => {
            var e = cmd as ProcessOptionTradeEndOfDayCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateParameters(e);
        },
        [typeof(UpdateOptionTradeDailyProfitTargetCommand).Name] = cmd => {
            var e = cmd as UpdateOptionTradeDailyProfitTargetCommand; return new List<ValidationError>()
                .ValidateCommandId(e!.CommandId, e.CommandName)
                .ValidateOptionTradeId(e.EntityId, e.CommandName);
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
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        return await _repo.LoadStateAsync(cmd);
    }

    /// <summary>
    /// Asynchronously saves the current state of the option trade actor in response to a command.
    /// </summary>
    /// <param name="context">The context for the actor command execution, providing access to actor metadata and runtime services. Cannot be
    /// null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="OptionTradeCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var optionTradeState = IsArgumentNull.Set((state as OptionTradeCommandState)!);
        await _repo.SaveStateAsync(context, optionTradeState, cmd);
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
            var cmdErrorEvent = await ex.SendErrorEventAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
            return new ServiceFailed<GuidResult>(cmdErrorEvent);
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
            try
            {
                var cmdErrorEvent = await ex.SendErrorEventAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context);
                return new ServiceFailed<GuidResult>(cmdErrorEvent);
            }
            catch (Exception fatalEx)
            {
                return CommandFailed(fatalEx, command);
            }
        }
    }
}
