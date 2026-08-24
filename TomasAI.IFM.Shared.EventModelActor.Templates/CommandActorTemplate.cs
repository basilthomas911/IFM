using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Templates.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor.Templates;

/// <summary>
/// Template for an event-sourced command actor. Add command parsers, handlers, and validators to the empty maps.
/// </summary>
public class CommandActorTemplate(
    ICommandActorContext<CommandActorTemplate> actorContext)
    : BaseEventSourceCommandActor<CommandActorTemplate>(
        actorContext.Logger,
        actorContext.ActorId)
{
    /// <summary>Gets the typed context owned by this actor.</summary>
    protected ICommandActorTemplateContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as ICommandActorTemplateContext, nameof(actorContext))!;

    /// <summary>Gets the actor mailbox name.</summary>
    public const string ActorName = "CommandActorTemplate";

    readonly IEventSourceActorDbContext _dbEventSource = IsArgumentNull.Set(actorContext.DbEventSource);
    IEventSourceActorStateRepository<CommandActorTemplateState> _repository = default!;

    static readonly Dictionary<string, Func<IActorMessage, ICommand>> _parseMap = [];

    static readonly Dictionary<string, Func<
        ICommand,
        ICommandActorContext<CommandActorTemplate>,
        CommandActorTemplateState,
        ServiceResult<GuidResult>>> _receiveMap = [];

    static readonly Dictionary<string, Func<ICommand, List<ValidationError>>> _validationMap = [];

    protected override ValueTask OnStartup(ICommandActorContext context)
    {
        IsArgumentNull.Check(context);
        _repository = IsArgumentNull.Set(
            context.Container.Resolve<IEventSourceActorStateRepository<CommandActorTemplateState>>());
        return ValueTask.CompletedTask;
    }

    protected override ICommand ParseMessage(ICommandActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Command, Name: ActorName }
            || !_parseMap.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} command from message: {message.Subject}");

        var command = parser.Invoke(message);
        IsArgumentNull.Check(command);
        return command;
    }

    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext context,
        IActorState state,
        ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);

        await _dbEventSource.InsertCommandLogAsync(
            command,
            DateTime.UtcNow,
            JsonConvert.SerializeObject(command));

        var templateState = IsArgumentNull.Set((state as CommandActorTemplateState)!);
        if (!_receiveMap.TryGetValue(command.GetType().Name, out var handler))
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} command from message: {command.Subject}");

        return handler.Invoke(command, actorContext.RouteTo(context), templateState);
    }

    protected override ValueTask OnValidateAsync(
        ICommandActorContext context,
        ActorThreadId threadId,
        ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);

        if (!_validationMap.TryGetValue(command.GetType().Name, out var validator))
            throw new InvalidOperationException(
                $"Unable to validate {ActorName} commands from message: {command.Subject}");

        validator.Invoke(command).ThrowCommandValidationExceptionOnAnyError(command.ErrorCode);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext context,
        ActorThreadId threadId,
        ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        return await _repository.LoadStateAsync(command);
    }

    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);

        var templateState = IsArgumentNull.Set((state as CommandActorTemplateState)!);
        await _repository.SaveStateAsync(context, templateState, command);
    }

    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(command);

            var errorEvent = await exception.SendErrorEventAsync<
                Events.CommandExceptionEvent,
                ActorEntityId>(ErrorType.Command, context);
            return new ServiceFailed<GuidResult>(errorEvent);
        }
        catch (Exception innerException)
        {
            actorContext.Logger.LogError(
                innerException,
                "Error handling exception for {ActorName} command in thread {ThreadId}: {OriginalExceptionMessage}",
                ActorName,
                threadId,
                exception.Message);

            try
            {
                var errorEvent = await exception.SendErrorEventAsync<
                    Events.CommandExceptionEvent,
                    ActorEntityId>(ErrorType.Command, context);
                return new ServiceFailed<GuidResult>(errorEvent);
            }
            catch (Exception fatalException)
            {
                return CommandFailed(fatalException);
            }
        }
    }
}

/// <summary>
/// Minimal state used by <see cref="CommandActorTemplate"/>. Add event application handlers when specializing the template.
/// </summary>
public sealed class CommandActorTemplateState
    : BaseEventSourceActorState<CommandActorTemplateState>,
        IEventSourceActorState<CommandActorTemplateState>
{
    /// <inheritdoc/>
    public override ActorThreadId Id { get; set; } = default!;

    protected override bool Apply(IEvent domainEvent) => false;
}
