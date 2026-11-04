using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog.Command.Actor;

public sealed class StrategyCatalogCommandActor(ICommandActorContext<StrategyCatalogCommandActor> context,
    StrategyCatalogService catalog, ILogger<StrategyCatalogCommandActor> logger)
    : BaseEventSourceCommandActor<StrategyCatalogCommandActor>(context, logger)
{
    public const string ActorName = StrategyCatalogLifecycleCommand.Actor;
    IEventProjector<StrategyCatalogCommandActor> Projector => Context.Container.Resolve<IEventProjector<StrategyCatalogCommandActor>>();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [SaveStrategyCatalogDraftCommand.Verb] = message => message.AsCommand<SaveStrategyCatalogDraftCommand>()!,
            [PublishStrategyCatalogVersionCommand.Verb] = message => message.AsCommand<PublishStrategyCatalogVersionCommand>()!,
            [RetireStrategyCatalogVersionCommand.Verb] = message => message.AsCommand<RetireStrategyCatalogVersionCommand>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(SaveStrategyCatalogDraftCommand)] = command =>
                new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
                    .ValidateEntityId(((SaveStrategyCatalogDraftCommand)command).EntityId, command.CommandName),
            [typeof(PublishStrategyCatalogVersionCommand)] = command =>
                new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
                    .ValidateEntityId(((PublishStrategyCatalogVersionCommand)command).EntityId, command.CommandName),
            [typeof(RetireStrategyCatalogVersionCommand)] = command =>
                new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
                    .ValidateEntityId(((RetireStrategyCatalogVersionCommand)command).EntityId, command.CommandName)
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, StrategyCatalogService, IEventProjector<StrategyCatalogCommandActor>, CancellationToken,
        ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, StrategyCatalogService, IEventProjector<StrategyCatalogCommandActor>, CancellationToken,
            ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(SaveStrategyCatalogDraftCommand)] = static (command, service, projector, token) =>
                ((SaveStrategyCatalogDraftCommand)command).ExecuteAsync(service, projector, token),
            [typeof(PublishStrategyCatalogVersionCommand)] = static (command, service, projector, token) =>
                ((PublishStrategyCatalogVersionCommand)command).ExecuteAsync(service, projector, token),
            [typeof(RetireStrategyCatalogVersionCommand)] = static (command, service, projector, token) =>
                ((RetireStrategyCatalogVersionCommand)command).ExecuteAsync(service, projector, token)
        };

    protected override ICommand ParseMessage(ICommandActorContext<StrategyCatalogCommandActor> services, IActorMessage message)
        => ParseMappedCommand(services, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<StrategyCatalogCommandActor> services, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<StrategyCatalogCommandActor> services,
        IActorState state, ICommand command) => ReceiveAsync(services, state, command, CancellationToken.None);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<StrategyCatalogCommandActor> services,
        IActorState state, ICommand command, CancellationToken cancellationToken)
        => ResolveMappedCommandHandler(command, _receiveMap)(command, catalog, Projector, cancellationToken);

    protected override async ValueTask OnStartup(ICommandActorContext<StrategyCatalogCommandActor> services)
        => await Projector.StartAsync(services).ConfigureAwait(false);

    protected override async ValueTask OnShutdown(ICommandActorContext<StrategyCatalogCommandActor> services)
        => await Projector.StopAsync().ConfigureAwait(false);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<StrategyCatalogCommandActor> services,
        ActorThreadId threadId, ICommand command, Exception error)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, error.Message));
}
