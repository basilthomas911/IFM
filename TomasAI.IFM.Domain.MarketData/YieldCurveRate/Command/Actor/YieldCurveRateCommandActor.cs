using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Application.Storage;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Actor;

/// <summary>
/// Represents an actor responsible for managing yield curve rate commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="YieldCurveRateCommandActor"/> is a specialized command actor designed to handle operations
/// related to yield curve rates. It processes commands such as adding, changing, removing, and importing yield curve rates,
/// validates the commands, and manages the actor's state. This actor relies on an event-sourced repository for state
/// persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="dbEventSource">The database context for event source operations.</param>
/// <param name="logger">The logger instance for logging operations.</param>
public class YieldCurveRateCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<YieldCurveRateCommandActor> logger)
    : BaseEventSourceCommandActor<YieldCurveRateCommandActor>(logger, new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "YieldCurveRateCommand";
    readonly IEventSourceActorDbContext _dbEventSource = IsArgumentNull.Set(dbEventSource);
    IEventSourceActorStateRepository<YieldCurveRateCommandState> _repo = default!;

    /// <summary>
    /// Performs initialization logic when the actor starts up.
    /// </summary>
    /// <remarks>This method resolves the required state repository from the dependency container and ensures
    /// that the base class startup logic is executed. Override this method to include additional startup logic specific
    /// to the actor.</remarks>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override ValueTask OnStartup(ICommandActorContext context)
    {
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<YieldCurveRateCommandState>>());
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a command instance for the specified actor context.
    /// </summary>
    /// <remarks>The command is logged asynchronously during validation, before validation is performed. This method
    /// expects the message subject to match a registered command verb for the actor.</remarks>
    /// <param name="context">The actor context used to resolve and process the command. Cannot be null.</param>
    /// <param name="message">The NATS message containing the command data to be parsed. Must have a subject and payload appropriate for
    /// command resolution.</param>
    /// <returns>An <see cref="ICommand"/> instance representing the parsed command from the message.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known command for the actor, or if command resolution
    /// fails.</exception>
    protected override ICommand ParseMessage(ICommandActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Command, Name: ActorName })
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        ICommand? command = msgSubject.Verb switch
        {
            AddYieldCurveRateCommand.Verb => message.AsCommand<AddYieldCurveRateCommand>(),
            ChangeYieldCurveRateCommand.Verb => message.AsCommand<ChangeYieldCurveRateCommand>(),
            RemoveYieldCurveRateCommand.Verb => message.AsCommand<RemoveYieldCurveRateCommand>(),
            ImportYieldCurveRatesCommand.Verb => message.AsCommand<ImportYieldCurveRatesCommand>(),
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}")
        };
        IsArgumentNull.Check(command);
        return command;
    }

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of YieldCurveRateCommandState. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains a ServiceResult wrapping a
    /// GuidResult with the command's unique identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var yieldCurveRateState = IsArgumentNull.Set((state as YieldCurveRateCommandState)!);
        _ = cmd switch
        {
            AddYieldCurveRateCommand command => command.Execute(yieldCurveRateState),
            ChangeYieldCurveRateCommand command => command.Execute(yieldCurveRateState),
            RemoveYieldCurveRateCommand command => command.Execute(yieldCurveRateState),
            ImportYieldCurveRatesCommand command => command.Execute(yieldCurveRateState),
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {cmd.Subject}")
        };
        return ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceOk<GuidResult>(new GuidResult(cmd.CommandId)));
    }

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <remarks>This method performs validation specific to the type of command being processed. It ensures
    /// that the command's identifiers and associated data meet the required criteria. If validation errors are
    /// detected, a <see cref="CommandValidationException"/> is thrown with the relevant error details.</remarks>
    /// <param name="context">The context in which the command is being executed, providing access to services and dependencies.</param>
    /// <param name="threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name="cmd">The command to be validated.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
        => OnValidateAsync(context, threadId, cmd, CancellationToken.None);

    protected override async ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        if (cancellationToken.CanBeCanceled)
            await _dbEventSource.InsertCommandLogAsync(
                cmd, DateTime.UtcNow, JsonConvert.SerializeObject(cmd), cancellationToken).ConfigureAwait(false);
        else
            await _dbEventSource.InsertCommandLogAsync(
                cmd, DateTime.UtcNow, JsonConvert.SerializeObject(cmd)).ConfigureAwait(false);
        var yieldCurveRateValidationRules = IsArgumentNull.Set(context.Container.Resolve<IValidationRules<YieldCurveRateReadModel>>());
        GetValidationErrors(cmd, yieldCurveRateValidationRules)
            .ThrowCommandValidationExceptionOnAnyError(cmd.ErrorCode);
    }

    static List<ValidationError> GetValidationErrors(
        ICommand command,
        IValidationRules<YieldCurveRateReadModel> validationRules)
        => command switch
        {
            AddYieldCurveRateCommand typedCommand => Validate(typedCommand, validationRules),
            ChangeYieldCurveRateCommand typedCommand => Validate(typedCommand, validationRules),
            RemoveYieldCurveRateCommand typedCommand => new List<ValidationError>(2)
                .ValidateCommandId(typedCommand.CommandId, typedCommand.CommandName)
                .ValidateValueDate(typedCommand.ValueDate, typedCommand.CommandName),
            ImportYieldCurveRatesCommand typedCommand => Validate(typedCommand, validationRules),
            _ => throw new InvalidOperationException(
                $"Unable to validate {ActorName} commands from message: {command.Subject}")
        };

    static List<ValidationError> Validate(
        AddYieldCurveRateCommand command,
        IValidationRules<YieldCurveRateReadModel> validationRules)
    {
        var errors = new List<ValidationError>(2)
            .ValidateCommandId(command.CommandId, command.CommandName);
        errors.AddRange(validationRules.Execute(command.YieldCurveRate));
        return errors;
    }

    static List<ValidationError> Validate(
        ChangeYieldCurveRateCommand command,
        IValidationRules<YieldCurveRateReadModel> validationRules)
    {
        var errors = new List<ValidationError>(2)
            .ValidateCommandId(command.CommandId, command.CommandName);
        errors.AddRange(validationRules.Execute(command.YieldCurveRate));
        return errors;
    }

    static List<ValidationError> Validate(
        ImportYieldCurveRatesCommand command,
        IValidationRules<YieldCurveRateReadModel> validationRules)
        => new List<ValidationError>()
            .ValidateCommandId(command.CommandId, command.CommandName)
            .ValidateImportDate(command.ImportDate, command.CommandName);

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
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        return await _repo.LoadStateAsync(cmd);
    }

    /// <summary>
    /// Asynchronously saves the current state of the yield curve rate actor in response to a command.
    /// </summary>
    /// <remarks>This method overrides the base implementation to persist the state specific to yield curve rate
    /// actors. The state must be of type <see cref="YieldCurveRateCommandState"/>; otherwise, an exception will be
    /// thrown. All parameters are required and must not be null.</remarks>
    /// <param name="context">The context for the actor command execution, providing access to actor metadata and runtime services. Cannot be
    /// null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see
    /// cref="YieldCurveRateCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var yieldCurveRateState = IsArgumentNull.Set((state as YieldCurveRateCommandState)!);
        return _repo.SaveStateAsync(context, yieldCurveRateState, cmd);
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
    /// <param name="command">The command that was being processed when the exception occurred.</param>
    /// <param name="ex">The exception that was thrown during command processing. Determines the type of error event to generate.</param>
    /// <returns>A failed service result containing a GUID result and error event details describing the failure. The result
    /// reflects the nature of the exception and the associated command context.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.LoadStateAsync(cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.SaveStateAsync(context, (YieldCurveRateCommandState)state, cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);
            IErrorEvent<YieldCurveRateEntityId> errorEvent = ex switch
            {
                AddYieldCurveRateException
                    => await ex.SendErrorEventAsync<YieldCurveRateAddedFailEvent, YieldCurveRateEntityId>(context, (command as AddYieldCurveRateCommand)!, YieldCurveRateAddedEvent.Actor, YieldCurveRateAddedEvent.Verb),
                ChangeYieldCurveRateException
                    => await ex.SendErrorEventAsync<YieldCurveRateChangedFailEvent, YieldCurveRateEntityId>(context, (command as ChangeYieldCurveRateCommand)!, YieldCurveRateChangedEvent.Actor, YieldCurveRateChangedEvent.Verb),
                RemoveYieldCurveRateException
                    => await ex.SendErrorEventAsync<YieldCurveRateRemovedFailEvent, YieldCurveRateEntityId>(context, (command as RemoveYieldCurveRateCommand)!, YieldCurveRateRemovedEvent.Actor, YieldCurveRateRemovedEvent.Verb),
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
            logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
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
