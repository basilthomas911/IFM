using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.FeedSessionPreparation.Function.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FeedSessionPreparation.Function.Actor;

public sealed class FeedSessionPreparationFunctionActor(
    IFunctionActorContext<FeedSessionPreparationFunctionActor> context,
    IEventSourceFunctionStateRepository<FeedSessionPreparationFunctionState, PrepareMarketDataFeedSessionCommand> repository,
    IValueDateProvider dates, IMarketDataApi marketData, ILogger<FeedSessionPreparationFunctionActor> logger)
    : BaseEventSourceFunctionActor<FeedSessionPreparationFunctionActor, PrepareMarketDataFeedSessionCommand, ActorEntityId, ActorEntityId,
        FeedSessionPreparationFunctionState, FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = PrepareMarketDataFeedSessionCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, PrepareMarketDataFeedSessionCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, PrepareMarketDataFeedSessionCommand>>(StringComparer.Ordinal)
        {
            [PrepareMarketDataFeedSessionCommand.Verb] = message => message.AsCommand<PrepareMarketDataFeedSessionCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<PrepareMarketDataFeedSessionCommand, IValueDateProvider, IMarketDataApi, CancellationToken, ValueTask<FeedSessionPreparationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<PrepareMarketDataFeedSessionCommand, IValueDateProvider, IMarketDataApi, CancellationToken, ValueTask<FeedSessionPreparationCompletedEvent>>>
        {
            [typeof(PrepareMarketDataFeedSessionCommand)] = static (request, dates, marketData, token) => request.ExecuteAsync(dates, marketData, token)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(PrepareMarketDataFeedSessionCommand)] = static request => ((PrepareMarketDataFeedSessionCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<PrepareMarketDataFeedSessionCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<PrepareMarketDataFeedSessionCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(PrepareMarketDataFeedSessionCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<PrepareMarketDataFeedSessionCommand>, TimeProvider,
        FunctionResult<FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<PrepareMarketDataFeedSessionCommand>, TimeProvider,
            FunctionResult<FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent>>>
        {
            [typeof(FeedSessionPreparationCompletedEvent)] = static (input, _) =>
                FunctionResult<FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent>.Complete((FeedSessionPreparationCompletedEvent)input.Outcome!),
            [typeof(FeedSessionPreparationFailedEvent)] = static (input, clock) =>
                FunctionResult<FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override PrepareMarketDataFeedSessionCommand ParseMessage(IFunctionActorContext<FeedSessionPreparationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<FeedSessionPreparationFunctionActor> services, ActorThreadId threadId,
        PrepareMarketDataFeedSessionCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(PrepareMarketDataFeedSessionCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<FeedSessionPreparationFunctionActor> services, FeedSessionPreparationFunctionState state,
        PrepareMarketDataFeedSessionCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, dates, marketData, cancellationToken).ConfigureAwait(false);
        return FunctionResult<FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<FeedSessionPreparationCompletedEvent, FeedSessionPreparationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<FeedSessionPreparationFunctionActor> services, FunctionEventContext<PrepareMarketDataFeedSessionCommand> input)
        => DispatchMappedFunctionEvent(input, TimeProvider.System, _eventMap);
}
