using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;using TomasAI.IFM.Domain.Trade.Shared;using TomasAI.IFM.Shared.EventModelActor;using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
public sealed class FuturesOptionTradeCommandState:BaseEventSourceActorState<FuturesOptionTradeCommandState>{public override ActorThreadId Id{get;set;}=default!;public EstablishedTradeDefinition?Current{get;private set;}protected override bool Apply(IEvent e){if(e is not OptionTradeChangedEvent x)return false;Current=x.State;return true;}}
