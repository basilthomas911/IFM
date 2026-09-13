using TomasAI.IFM.Domain.Trade.Shared.Futures;using TomasAI.IFM.Domain.Trade.Shared;using TomasAI.IFM.Shared.EventModelActor;using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Command.State;
public sealed class FuturesTradeCommandState:BaseEventSourceActorState<FuturesTradeCommandState>{public override ActorThreadId Id{get;set;}=default!;public EstablishedTradeDefinition? Current{get;private set;}protected override bool Apply(IEvent e){if(e is not FuturesTradeChangedEvent x)return false;Current=x.State;return true;}}
