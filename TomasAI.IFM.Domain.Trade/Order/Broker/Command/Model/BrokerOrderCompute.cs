using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Model;
/// <summary>The proposed broker-order definition or a business rejection, with replay disposition.</summary>
internal sealed record BrokerOrderCompute(bool Accepted, BrokerOrderDefinition? BrokerOrderDefinition, string RejectionReason, bool IsReplay = false)
{
    /// <summary>Checks that accepted business data belongs to the commanded broker order.</summary>
    public bool IsValidFor(BrokerOrderId brokerOrderId) => Accepted && BrokerOrderDefinition is { } brokerOrder && brokerOrder.Id.IsValid && brokerOrder.Id == brokerOrderId && brokerOrder.Revision > 0;
    /// <summary>Accepts a proposed definition without modifying authoritative state.</summary>
    public static BrokerOrderCompute Accept(BrokerOrderDefinition brokerOrderDefinition) => new(true, brokerOrderDefinition, string.Empty);
    /// <summary>Acknowledges an already-applied intent without another event or broker dispatch.</summary>
    public static BrokerOrderCompute Replay(BrokerOrderDefinition brokerOrderDefinition) => new(true, brokerOrderDefinition, string.Empty, true);
    /// <summary>Rejects the proposed change with its existing business reason.</summary>
    public static BrokerOrderCompute Reject(string rejectionReason) => new(false, null, rejectionReason);
}
