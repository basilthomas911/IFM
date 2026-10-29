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
    protected override ICommand ParseMessage(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, IActorMessage message) => message.AsCommand<AppendMarketDataFeedRecoveryCommand>()!;
    protected override ValueTask OnValidateAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context,ActorThreadId thread,ICommand command)
    {
        var request=(AppendMarketDataFeedRecoveryCommand)command;
        var fact=RecoveryRecord.Parse(request.Payload,request.Digest);
        if(request.EntityId.Value!=fact.OperationId || request.AttemptId==Guid.Empty ||
            request.Subject!=new ActorSubject(ActorType.Command,ActorName,AppendMarketDataFeedRecoveryCommand.Verb,request.EntityId.Format()) ||
            request.AttemptStartedAtUtc.Kind!=DateTimeKind.Utc || request.RequestDeadlineUtc.Kind!=DateTimeKind.Utc ||
            request.RequestDeadlineUtc<=request.AttemptStartedAtUtc)
            throw new InvalidDataException("Invalid recovery append route/attempt/deadline.");
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
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, IActorState state, ICommand command)
    {
        var request = (AppendMarketDataFeedRecoveryCommand)command;
        if (request.RequestDeadlineUtc <= DateTime.UtcNow) throw new TimeoutException("Recovery audit attempt expired before append.");
        ((MarketDataFeedRecoveryState)state).Accept(request);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new(new GuidResult(command.CommandId)));
    }
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<MarketDataFeedRecoveryCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        logger.LogError(ex, "Recovery audit append rejected {CommandId}", command.CommandId);
        return ValueTask.FromResult(new ServiceResult<GuidResult>(5090, ex.ToString()));
    }
}
