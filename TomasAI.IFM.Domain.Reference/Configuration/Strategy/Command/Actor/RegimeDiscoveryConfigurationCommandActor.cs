using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Extensions;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

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

    static readonly IReadOnlyDictionary<string, Action<ICommand>> _validationMap =
        new Dictionary<string, Action<ICommand>>(StringComparer.Ordinal)
        {
            [nameof(CreateRegimeDiscoveryParameterSetCommand)] = command => ValidateCreate((CreateRegimeDiscoveryParameterSetCommand)command),
            [nameof(PublishRegimeDiscoveryParameterSetCommand)] = ValidateCommon,
            [nameof(RetireRegimeDiscoveryParameterSetCommand)] = ValidateCommon
        };

    static readonly IReadOnlyDictionary<string, Func<ICommand, ICommandActorContext,
        RegimeDiscoveryConfigurationCommandState, Task<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<string, Func<ICommand, ICommandActorContext,
            RegimeDiscoveryConfigurationCommandState, Task<ServiceResult<GuidResult>>>>(StringComparer.Ordinal)
        {
            [nameof(CreateRegimeDiscoveryParameterSetCommand)] = (command, context, state) =>
                ((CreateRegimeDiscoveryParameterSetCommand)command).ExecuteAsync(context, state),
            [nameof(PublishRegimeDiscoveryParameterSetCommand)] = (command, context, state) =>
                ((PublishRegimeDiscoveryParameterSetCommand)command).ExecuteAsync(context, state),
            [nameof(RetireRegimeDiscoveryParameterSetCommand)] = (command, context, state) =>
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
    protected override ICommand ParseMessage(ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName } ||
            !_parseMap.TryGetValue(message.Subject.Verb, out var parse))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return parse(message);
    }

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        ActorThreadId threadId, ICommand command)
    {
        if (!_validationMap.TryGetValue(command.GetType().Name, out var validate))
            throw new InvalidOperationException($"Unsupported configuration command {command.GetType().Name}.");
        validate(command);
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
        if (!_receiveMap.TryGetValue(command.GetType().Name, out var receive))
            throw new InvalidOperationException($"Unsupported configuration command {command.GetType().Name}.");
        return await receive(command, context, (RegimeDiscoveryConfigurationCommandState)state).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context,
        ActorThreadId threadId, ICommand command, Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceResult<GuidResult>(command?.ErrorCode ?? 33000, ex.Message));

    static void ValidateCreate(CreateRegimeDiscoveryParameterSetCommand command)
    {
        ValidateCommon(command);
        var errors = new RegimeDiscoveryParameterSetValidationRules().Execute(command.ParameterSet);
        if (errors.Length != 0)
            throw new ArgumentException(string.Join("; ", errors.Select(x => x.ErrorMessage)), nameof(command));
        if (command.EntityId.ParameterSetId != command.ParameterSet.ParameterSetId ||
            command.EntityId.Version != command.ParameterSet.Version)
            throw new ArgumentException("Entity identity must match the parameter payload.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CreatedBy);
    }

    static void ValidateCommon(ICommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new ArgumentException("CommandId is required.", nameof(command));
        var entity = ((ICommand<RegimeDiscoveryParameterSetEntityId>)command).EntityId;
        if (entity.ParameterSetId == Guid.Empty || entity.Version <= 0)
            throw new ArgumentException("A parameter-set identity and positive version are required.", nameof(command));
    }

    static IRegimeDiscoveryConfigurationCommandContext Typed(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> context)
        => context as IRegimeDiscoveryConfigurationCommandContext
           ?? throw new ArgumentException("A typed configuration context is required.", nameof(context));
}
