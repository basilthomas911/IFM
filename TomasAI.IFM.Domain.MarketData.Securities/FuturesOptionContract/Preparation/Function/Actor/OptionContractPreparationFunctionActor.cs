using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Preparation.Function.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Preparation.Function.Actor;

public sealed class OptionContractPreparationFunctionActor(
    IFunctionActorContext<OptionContractPreparationFunctionActor> context,
    IEventSourceFunctionStateRepository<OptionContractPreparationFunctionState, PrepareOptionContractsCommand> repository,
    IActorService actors, ILogger<OptionContractPreparationFunctionActor> logger)
    : BaseEventSourceFunctionActor<OptionContractPreparationFunctionActor, PrepareOptionContractsCommand, ActorEntityId, ActorEntityId,
        OptionContractPreparationFunctionState, OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = PrepareOptionContractsCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, PrepareOptionContractsCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, PrepareOptionContractsCommand>>(StringComparer.Ordinal)
        {
            [PrepareOptionContractsCommand.Verb] = message => message.AsCommand<PrepareOptionContractsCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<PrepareOptionContractsCommand, IActorService, CancellationToken, ValueTask<OptionContractPreparationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<PrepareOptionContractsCommand, IActorService, CancellationToken, ValueTask<OptionContractPreparationCompletedEvent>>>
        {
            [typeof(PrepareOptionContractsCommand)] = static (request, actors, token) => request.ExecuteAsync(actors, token)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(PrepareOptionContractsCommand)] = static request => ((PrepareOptionContractsCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<PrepareOptionContractsCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<PrepareOptionContractsCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(PrepareOptionContractsCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<PrepareOptionContractsCommand>, TimeProvider,
        FunctionResult<OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<PrepareOptionContractsCommand>, TimeProvider,
            FunctionResult<OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent>>>
        {
            [typeof(OptionContractPreparationCompletedEvent)] = static (input, _) =>
                FunctionResult<OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent>.Complete((OptionContractPreparationCompletedEvent)input.Outcome!),
            [typeof(OptionContractPreparationFailedEvent)] = static (input, clock) =>
                FunctionResult<OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override PrepareOptionContractsCommand ParseMessage(IFunctionActorContext<OptionContractPreparationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<OptionContractPreparationFunctionActor> services, ActorThreadId threadId,
        PrepareOptionContractsCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(PrepareOptionContractsCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<OptionContractPreparationFunctionActor> services, OptionContractPreparationFunctionState state,
        PrepareOptionContractsCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, actors, cancellationToken).ConfigureAwait(false);
        return FunctionResult<OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<OptionContractPreparationCompletedEvent, OptionContractPreparationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<OptionContractPreparationFunctionActor> services, FunctionEventContext<PrepareOptionContractsCommand> input)
        => DispatchMappedFunctionEvent(input, TimeProvider.System, _eventMap);
}
