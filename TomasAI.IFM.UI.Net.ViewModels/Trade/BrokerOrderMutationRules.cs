using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.UI.Net.ViewModels.Trade;
/// <summary>UI eligibility for the existing WinForms working-order price and cancellation actions.</summary>
public static class BrokerOrderMutationRules
{
 public static bool CanMutate(BrokerOrderStatus status,OrderExecutionStatus? executionStatus,TradeOrderComponentDefinition component,bool awaitingAcknowledgement,bool readOnly=false)=>!readOnly&&!awaitingAcknowledgement&&(status is BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled)&&executionStatus is not (OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected)&&component.TickIncrement is >0&&component.MinimumSignedNetDebitLimit.HasValue&&component.MaximumSignedNetDebitLimit.HasValue&&component.MinimumSignedNetDebitLimit<=component.MaximumSignedNetDebitLimit;
}
