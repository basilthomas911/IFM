using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Actor;

/// <summary>Owns private event-sourced processing state for one Regime Discovery workflow entity.</summary>
public sealed class RegimeDiscoveryCommandActor(
    ICommandActorContext<RegimeDiscoveryCommandActor> actorContext)
    : BaseEventSourceCommandActor<RegimeDiscoveryCommandActor>(actorContext, GetLogger(actorContext))
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [StartRegimeDiscoveryPipelineCommand.Verb] =
                message => message.AsCommand<StartRegimeDiscoveryPipelineCommand>()!
        };

    static readonly IReadOnlyDictionary<string, Action<ICommand>> _validationMap =
        new Dictionary<string, Action<ICommand>>(StringComparer.Ordinal)
        {
            [typeof(StartRegimeDiscoveryPipelineCommand).Name] = command =>
                Validate((StartRegimeDiscoveryPipelineCommand)command)
        };

    static readonly IReadOnlyDictionary<string,
        Func<ICommand, ICommandActorContext<RegimeDiscoveryCommandActor>, RegimeDiscoveryCommandState,
            Task<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<string,
            Func<ICommand, ICommandActorContext<RegimeDiscoveryCommandActor>, RegimeDiscoveryCommandState,
                Task<ServiceResult<GuidResult>>>>(StringComparer.Ordinal)
        {
            [typeof(StartRegimeDiscoveryPipelineCommand).Name] = (command, context, state) =>
                ((StartRegimeDiscoveryPipelineCommand)command).ExecuteAsync(context, state)
        };

    /// <summary>Gets the Command actor name used for dependency injection and routing.</summary>
    public const string ActorName = StartRegimeDiscoveryPipelineCommand.Actor;

    static Microsoft.Extensions.Logging.ILogger<RegimeDiscoveryCommandActor> GetLogger(
        ICommandActorContext<RegimeDiscoveryCommandActor> context)
        => (context as IRegimeDiscoveryCommandContext)?.Logger
           ?? throw new InvalidOperationException(
               $"{nameof(context)} must implement {nameof(IRegimeDiscoveryCommandContext)}.");

    IRegimeDiscoveryCommandContext ActorContext => Context as IRegimeDiscoveryCommandContext
        ?? throw new InvalidOperationException(
            $"{nameof(Context)} must implement {nameof(IRegimeDiscoveryCommandContext)}.");

    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<RegimeDiscoveryCommandActor> context)
        => await ActorContext.EventProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(ICommandActorContext<RegimeDiscoveryCommandActor> context)
        => await ActorContext.EventProjector.StopAsync().ConfigureAwait(false);

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject.ActorType != ActorType.Command ||
            !string.Equals(message.Subject.Name, ActorName, StringComparison.Ordinal) ||
            !_parseMap.TryGetValue(message.Subject.Verb, out var parse))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        return parse(message);
    }

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        if (!_validationMap.TryGetValue(command.GetType().Name, out var validate))
            throw new InvalidOperationException($"Unsupported Regime Discovery command: {command.GetType().Name}");
        validate(command);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
        => await ActorContext.StateRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command)
        => await ActorContext.StateRepository.SaveStateAsync(
            context, (RegimeDiscoveryCommandState)state, command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        IActorState state,
        ICommand command)
    {
        if (!_receiveMap.TryGetValue(command.GetType().Name, out var receive))
            throw new InvalidOperationException($"Unsupported Regime Discovery command: {command.GetType().Name}");
        return await receive(command, context, (RegimeDiscoveryCommandState)state).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<RegimeDiscoveryCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceResult<GuidResult>(command?.ErrorCode ?? StartRegimeDiscoveryPipelineCommand.ErrorId,
                ex.Message));

    static void Validate(StartRegimeDiscoveryPipelineCommand command)
    {
        var errors = new List<string>();
        if (command.CommandId == Guid.Empty)
            errors.Add("CommandId is required.");
        if (string.IsNullOrWhiteSpace(command.Subject.EntityId))
            errors.Add("Subject.EntityId is required.");
        if (command.InputWorkflowRevision <= 0)
            errors.Add("InputWorkflowRevision must be positive.");
        errors.AddRange(new StrategyWorkflowIdValidationRules().Execute(command.WorkflowId)
            .Select(value => value.ErrorMessage));
        errors.AddRange(new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(command.EntityId)
            .Select(value => value.ErrorMessage));
        errors.AddRange(new RegimeDiscoveryParameterSetValidationRules().Execute(command.ParameterSet)
            .Select(value => value.ErrorMessage));
        if (command.TargetHorizon != command.ParameterSet.TargetHorizon ||
            command.TargetHorizon != command.TriggerEvent.EntityId.TimePeriod)
            errors.Add("TargetHorizon must match the parameter set and trigger ITI timeframe.");
        if (!IsSha256(command.ParameterPayloadSha256))
            errors.Add("ParameterPayloadSha256 must contain exactly 64 hexadecimal characters.");
        else if (!string.Equals(command.ParameterPayloadSha256,
                     RegimeDiscoveryParameterPayload.ComputeSha256(command.ParameterSet),
                     StringComparison.OrdinalIgnoreCase))
            errors.Add("ParameterPayloadSha256 must match the canonical parameter payload.");
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("; ", errors), nameof(command));
    }

    static bool IsSha256(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
