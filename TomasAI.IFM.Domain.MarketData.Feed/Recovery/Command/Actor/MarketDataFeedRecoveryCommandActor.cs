using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.Actor;
/// <summary>Event-sourced append-only observer, partitioned by native OperationId.</summary>
public sealed class MarketDataFeedRecoveryCommandActor(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context,
    IEventSourceActorStateRepository<MarketDataFeedRecoveryState> repository,
    IEventProjector<MarketDataFeedRecoveryCommandActor> projector, ILogger<MarketDataFeedRecoveryCommandActor> logger)
    : BaseEventSourceCommandActor<MarketDataFeedRecoveryCommandActor>(context, logger)
{
    public const string ActorName = AppendMarketDataFeedRecoveryCommand.Actor;
    protected override ValueTask OnStartup(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context) => projector.StartAsync(context);
    protected override ValueTask OnShutdown(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context) => projector.StopAsync();
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [AppendMarketDataFeedRecoveryCommand.Verb] = static message => message.AsCommand<AppendMarketDataFeedRecoveryCommand>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(AppendMarketDataFeedRecoveryCommand)] = static command =>
                ((AppendMarketDataFeedRecoveryCommand)command).Validate()
                    .ValidateCommandId(command.CommandId, command.CommandName)
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, MarketDataFeedRecoveryState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, MarketDataFeedRecoveryState, ServiceResult<GuidResult>>>
        {
            [typeof(AppendMarketDataFeedRecoveryCommand)] = static (command, state) =>
                ((AppendMarketDataFeedRecoveryCommand)command).Execute(state)
        };

    protected override ICommand ParseMessage(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, ActorThreadId thread, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, ActorThreadId threadId, ICommand command) =>
        await repository.LoadStateAsync(command).ConfigureAwait(false);
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) =>
        repository.SaveStateAsync(context, (MarketDataFeedRecoveryState)state, command);
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken token) =>
        await repository.LoadStateAsync(command,token).ConfigureAwait(false);
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken token) =>
        repository.SaveStateAsync(context,(MarketDataFeedRecoveryState)state,command,token);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, IActorState state, ICommand command) =>
        ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (MarketDataFeedRecoveryState)state));

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        logger.LogError(ex, "Recovery audit append rejected {CommandId}", command.CommandId);
        return ValueTask.FromResult(new ServiceResult<GuidResult>(5090, ex.ToString()));
    }
}
