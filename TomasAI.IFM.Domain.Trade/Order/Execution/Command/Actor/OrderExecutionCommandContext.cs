using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;

public interface IOrderExecutionCommandContext:ICommandActorContext<OrderExecutionCommandActor>
{ IDbContextFactory DbFactory{get;} IEventSourceActorDbContext DbEventSource{get;} IDurableReplayQueue DurableReplayQueue{get;} IBlackboardService BlackboardService{get;} IActorService ActorService{get;} IEventProjector<OrderExecutionCommandActor> EventProjector{get;} IEventSourceActorStateRepository<OrderExecutionCommandState> StateRepository{get;} ILogger<OrderExecutionCommandActor> Logger{get;} }
public sealed class OrderExecutionCommandContext:CommandActorContext,ICommandActorContext<OrderExecutionCommandActor>,IOrderExecutionCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> eventDb;readonly Lazy<IDurableReplayQueue> replay;readonly Lazy<IActorService> actors;readonly Lazy<IEventProjector<OrderExecutionCommandActor>> projector;readonly Lazy<IEventSourceActorStateRepository<OrderExecutionCommandState>> repository;
    public OrderExecutionCommandContext(IActorSupervisor supervisor,IDbContextFactory dbFactory,IBlackboardService blackboard,ILogger<OrderExecutionCommandActor> logger):base(supervisor,new(ActorType.Command,OrderExecutionCommandActor.ActorName)){DbFactory=dbFactory;BlackboardService=blackboard;Logger=logger;eventDb=R<IEventSourceActorDbContext>();replay=R<IDurableReplayQueue>();actors=R<IActorService>();projector=R<IEventProjector<OrderExecutionCommandActor>>();repository=R<IEventSourceActorStateRepository<OrderExecutionCommandState>>();}
    public IDbContextFactory DbFactory{get;}public IBlackboardService BlackboardService{get;}public ILogger<OrderExecutionCommandActor> Logger{get;}public IEventSourceActorDbContext DbEventSource=>eventDb.Value;public IDurableReplayQueue DurableReplayQueue=>replay.Value;public IActorService ActorService=>actors.Value;public IEventProjector<OrderExecutionCommandActor> EventProjector=>projector.Value;public IEventSourceActorStateRepository<OrderExecutionCommandState> StateRepository=>repository.Value;Lazy<T> R<T>()where T:class=>new(()=>Container.Resolve<T>());
}
