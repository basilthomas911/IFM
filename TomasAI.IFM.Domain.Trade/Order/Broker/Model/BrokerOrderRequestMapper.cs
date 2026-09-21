using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;

/// <summary>Converts one immutable Portfolio-approved component into a broker-neutral request.</summary>
public static class BrokerOrderRequestMapper
{
    /// <summary>Builds one logical BrokerOrder; old definitions and incomplete approvals fail closed.</summary>
    public static bool TryCreate(TradeOrderDefinition order, Guid executionAttemptId, Guid componentId,
        Guid operationId, out BrokerOrderRequest? request, out string reason)
    {
        request = null;
        reason = string.Empty;
        if (order is null || order.SchemaVersion < 4 || !order.Id.IsValid || order.Status is not (TradeOrderStatus.Approved or TradeOrderStatus.Ready or TradeOrderStatus.Executing) ||
            order.PositionType is not (TradeOrderPositionType.Opening or TradeOrderPositionType.Closing) ||
            order.PortfolioApprovalId == Guid.Empty || executionAttemptId == Guid.Empty || operationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(order.BrokerAccountAlias) || order.BrokerEnvironment == TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment.Unknown ||
            string.IsNullOrWhiteSpace(order.DefinitionHash) || string.IsNullOrWhiteSpace(order.MicroExecutionProfileHash))
        {
            reason = "BO.APPROVAL.INCOMPLETE";
            return false;
        }
        var component = order.Components.SingleOrDefault(c => c.ComponentId == componentId);
        if (component is null || component.ReservedTradeId <= 0 || component.SignedNetDebitLimit is null ||
            component.MinimumSignedNetDebitLimit is null || component.MaximumSignedNetDebitLimit is null || component.TickIncrement is null ||
            component.Legs.Length == 0 || component.Legs.Any(l => l.TradeLegId == Guid.Empty || string.IsNullOrWhiteSpace(l.ContractId) || l.SignedQuantity == 0 || l.CashMultiplier <= 0))
        {
            reason = "BO.COMPONENT.INCOMPLETE";
            return false;
        }
        var shape = component.StrategyKind switch
        {
            TradeStrategyKind.FuturesOutright when component.Legs.Length == 1 => BrokerOrderShape.FuturesOutright,
            TradeStrategyKind.VerticalSpread when component.Legs.Length == 2 => BrokerOrderShape.VerticalSpread,
            TradeStrategyKind.IronCondor when component.Legs.Length == 4 => BrokerOrderShape.IronCondor,
            _ => BrokerOrderShape.Unknown
        };
        if (shape == BrokerOrderShape.Unknown)
        {
            reason = "BO.SHAPE.UNSUPPORTED";
            return false;
        }
        var environment = order.BrokerEnvironment switch
        {
            TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment.Emulator => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Emulator,
            TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment.Paper => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Paper,
            TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment.Live => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Live,
            _ => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Unknown
        };
        // Unknown is the wire default for definitions written before these append-only fields existed.
        var orderType = order.BrokerOrderType switch
        {
            TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType.Unknown or TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType.Limit => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType.Limit,
            TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType.Market => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType.Market,
            _ => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType.Unknown
        };
        var algorithm = order.BrokerAlgorithm switch
        {
            TomasAI.IFM.Domain.Trade.Shared.BrokerAlgorithm.None => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerAlgorithm.None,
            TomasAI.IFM.Domain.Trade.Shared.BrokerAlgorithm.Adaptive => global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerAlgorithm.Adaptive,
            _ => (global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerAlgorithm)byte.MaxValue
        };
        if (environment == global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Unknown ||
            orderType == global::TomasAI.IFM.Application.TradeBroker.Contracts.BrokerOrderType.Unknown ||
            !Enum.IsDefined(algorithm))
        {
            reason = "BO.EXECUTION_SELECTION.UNSUPPORTED";
            return false;
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', component.Legs.Select(l =>
            $"{l.TradeLegId:N}:{l.ContractId}:{l.SignedQuantity}:{l.Expiry}:{l.Strike}:{l.PutCall}")))));
        request = new BrokerOrderRequest(order.BrokerAccountAlias, environment,
            $"{new OrderExecutionId(order.Id, executionAttemptId).Format()}.{componentId:N}",
            operationId, componentId, shape, order.PositionType == TradeOrderPositionType.Closing,
            [.. component.Legs.Select(l => new BrokerOrderLeg(l.TradeLegId, l.ContractId, l.SignedQuantity, l.Strike, l.Expiry, l.PutCall, l.CashMultiplier))],
            component.SignedNetDebitLimit.Value, component.MinimumSignedNetDebitLimit.Value,
            component.MaximumSignedNetDebitLimit.Value, component.TickIncrement.Value,
            order.ValidUntilUtc, order.DefinitionHash, fingerprint, order.RequiredCapital, order.MaximumLoss,
            orderType, algorithm);
        return true;
    }
}
