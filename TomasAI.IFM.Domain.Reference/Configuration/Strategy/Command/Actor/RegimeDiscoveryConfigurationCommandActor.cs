using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Validation;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Actor;

/// <summary>Owns the event-sourced lifecycle of immutable Regime Discovery parameter-set versions.</summary>
public sealed class RegimeDiscoveryConfigurationCommandActor(
    ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> actorContext)
    : BaseEventSourceCommandActor<RegimeDiscoveryConfigurationCommandActor>(actorContext, Typed(actorContext).Logger)
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [CreateRegimeDiscoveryParameterSetCommand.Verb] = message => message.AsCommand<CreateRegimeDiscoveryParameterSetCommand>()!,
            [PublishRegimeDiscoveryParameterSetCommand.Verb] = message => message.AsCommand<PublishRegimeDiscoveryParameterSetCommand>()!,
            [RetireRegimeDiscoveryParameterSetCommand.Verb] = message => message.AsCommand<RetireRegimeDiscoveryParameterSetCommand>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CreateRegimeDiscoveryParameterSetCommand)] = command =>
            {
                var create = (CreateRegimeDiscoveryParameterSetCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(create.CommandId, create.CommandName)
                    .ValidateRegimeDiscoveryConfigurationId(create.EntityId)
                    .ValidateRegimeDiscoveryCreation(create);
            },
            [typeof(PublishRegimeDiscoveryParameterSetCommand)] = command =>
            {
                var publish = (PublishRegimeDiscoveryParameterSetCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(publish.CommandId, publish.CommandName)
                    .ValidateRegimeDiscoveryConfigurationId(publish.EntityId);
            },
            [typeof(RetireRegimeDiscoveryParameterSetCommand)] = command =>
            {
                var retire = (RetireRegimeDiscoveryParameterSetCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(retire.CommandId, retire.CommandName)
                    .ValidateRegimeDiscoveryConfigurationId(retire.EntityId);
            }
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext,
        RegimeDiscoveryConfigurationCommandState, Task<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, ICommandActorContext,
            RegimeDiscoveryConfigurationCommandState, Task<ServiceResult<GuidResult>>>>()
        {
            [typeof(CreateRegimeDiscoveryParameterSetCommand)] = (command, context, state) =>
                ((CreateRegimeDiscoveryParameterSetCommand)command).ExecuteAsync(context, state),
            [typeof(PublishRegimeDiscoveryParameterSetCommand)] = (command, context, state) =>
                ((PublishRegimeDiscoveryParameterSetCommand)command).ExecuteAsync(context, state),
            [typeof(RetireRegimeDiscoveryParameterSetCommand)] = (command, context, state) =>
                ((RetireRegimeDiscoveryParameterSetCommand)command).ExecuteAsync(context, state)
        };

    /// <summary>Gets the Command actor name.</summary>
    public const string ActorName = CreateRegimeDiscoveryParameterSetCommand.Actor;
    IRegimeDiscoveryConfigurationCommandContext ActorContext { get; } = Typed(actorContext);

    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context)
        => await ActorContext.EventProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context)
        => await ActorContext.EventProjector.StopAsync().ConfigureAwait(false);

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        ActorThreadId threadId, ICommand command)
        => await ActorContext.StateRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command)
        => await ActorContext.StateRepository.SaveStateAsync(context,
            (RegimeDiscoveryConfigurationCommandState)state, command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        IActorState state, ICommand command)
    {
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return await receive(command, context, (RegimeDiscoveryConfigurationCommandState)state).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        ActorThreadId threadId, ICommand command, Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceResult<GuidResult>(command?.ErrorCode ?? 33000, ex.Message));

    static IRegimeDiscoveryConfigurationCommandContext Typed(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context)
        => context as IRegimeDiscoveryConfigurationCommandContext
           ?? throw new ArgumentException("A typed configuration context is required.", nameof(context));
}
