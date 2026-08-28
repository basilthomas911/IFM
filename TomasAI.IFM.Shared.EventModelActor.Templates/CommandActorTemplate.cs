using Microsoft.Extensions.Logging;
using NATS.Client.Core;
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
    : BaseEventSourceCommandActor<CommandActorTemplate>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the typed context owned by this actor.</summary>
    protected ICommandActorTemplateContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as ICommandActorTemplateContext, nameof(actorContext))!;

    /// <summary>Gets the actor mailbox name.</summary>
    public const string ActorName = "CommandActorTemplate";

    IEventSourceActorStateRepository<CommandActorTemplateState> _repository = default!;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>();

    static readonly IReadOnlyDictionary<Type, Func<
        ICommand,
        ICommandActorContext<CommandActorTemplate>,
        CommandActorTemplateState,
        ServiceResult<GuidResult>>> _receiveMap =
            new Dictionary<Type, Func<
                ICommand,
                ICommandActorContext<CommandActorTemplate>,
                CommandActorTemplateState,
                ServiceResult<GuidResult>>>();

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>();

    protected override ValueTask OnStartup(ICommandActorContext<CommandActorTemplate> context)
    {
        IsArgumentNull.Check(context);
        _repository = IsArgumentNull.Set(
            context.Container.Resolve<IEventSourceActorStateRepository<CommandActorTemplateState>>());
        return ValueTask.CompletedTask;
    }

    protected override ICommand ParseMessage(
        ICommandActorContext<CommandActorTemplate> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<CommandActorTemplate> context,
        IActorState state,
        ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(command);

        var templateState = IsArgumentNull.Set((state as CommandActorTemplateState)!);
        var handler = ResolveMappedCommandHandler(command, _receiveMap);

        return ValueTask.FromResult(handler.Invoke(command, context, templateState));
    }

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<CommandActorTemplate> context,
        ActorThreadId threadId,
        ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);

        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<CommandActorTemplate> context,
        ActorThreadId threadId,
        ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        return await _repository.LoadStateAsync(command);
    }

    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<CommandActorTemplate> context,
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
        ICommandActorContext<CommandActorTemplate> context,
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
