using TomasAI.IFM.Application.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
public sealed class OrderExecutionStateRepository(IEventSourceActorStateFactory stateFactory,IEventSourceActorDbContext eventSource,IActorService actors,IEventProjector<OrderExecutionCommandActor> projector,ILogger<OrderExecutionStateRepository> logger):BaseEventSourceActorRepository(stateFactory,eventSource,actors,logger),IEventSourceActorStateRepository<OrderExecutionCommandState>
{public ValueTask<OrderExecutionCommandState> LoadStateAsync(ICommand c)=>new(LoadStateAsync<OrderExecutionCommandState>(c));public async ValueTask SaveStateAsync(ICommandActorContext x,OrderExecutionCommandState s,ICommand c)=>await SaveStateAndDenormalizeEventsAsync(x,s,c);protected override ValueTask DenormalizeEventsAsync(ICommandActorContext c,DomainEventCollection e)=>projector.DomainEventsProjectionAsync(e);}
