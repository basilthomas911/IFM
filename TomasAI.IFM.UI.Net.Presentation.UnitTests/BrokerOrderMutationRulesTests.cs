using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
public sealed class BrokerOrderMutationRulesTests
{
 static readonly TradeOrderComponentDefinition Component=new(){TickIncrement=.05m,MinimumSignedNetDebitLimit=-10m,MaximumSignedNetDebitLimit=0m};
 [Theory][InlineData(BrokerOrderStatus.Working,true)][InlineData(BrokerOrderStatus.PartiallyFilled,true)][InlineData(BrokerOrderStatus.Filled,false)][InlineData(BrokerOrderStatus.Cancelled,false)][InlineData(BrokerOrderStatus.Rejected,false)][InlineData(BrokerOrderStatus.UpdatePending,false)][InlineData(BrokerOrderStatus.CancelPending,false)]public void Only_working_and_partially_filled_orders_are_mutable(BrokerOrderStatus state,bool expected)=>Assert.Equal(expected,BrokerOrderMutationRules.CanMutate(state,null,Component,false));
 [Theory][InlineData(OrderExecutionStatus.Filled)][InlineData(OrderExecutionStatus.Cancelled)][InlineData(OrderExecutionStatus.Rejected)]public void Terminal_execution_blocks_stale_working_status(OrderExecutionStatus state)=>Assert.False(BrokerOrderMutationRules.CanMutate(BrokerOrderStatus.Working,state,Component,false));
 [Fact]public void Pending_acknowledgement_and_read_only_views_block_actions(){Assert.False(BrokerOrderMutationRules.CanMutate(BrokerOrderStatus.Working,null,Component,true));Assert.False(BrokerOrderMutationRules.CanMutate(BrokerOrderStatus.Working,null,Component,false,true));}
 [Fact]public void Missing_invalid_bounds_or_tick_block_actions(){Assert.False(BrokerOrderMutationRules.CanMutate(BrokerOrderStatus.Working,null,Component with{TickIncrement=0},false));Assert.False(BrokerOrderMutationRules.CanMutate(BrokerOrderStatus.Working,null,Component with{MinimumSignedNetDebitLimit=null},false));Assert.False(BrokerOrderMutationRules.CanMutate(BrokerOrderStatus.Working,null,Component with{MaximumSignedNetDebitLimit=null},false));Assert.False(BrokerOrderMutationRules.CanMutate(BrokerOrderStatus.Working,null,Component with{MinimumSignedNetDebitLimit=1},false));}
}
