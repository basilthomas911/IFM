using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Shared deterministic identity for one workflow-approved opening, including expiry reconciliation.</summary>
public static class StrategyOpeningExecutionIdentity
{
    public static Guid For(TradeOrderId order, Guid workflow) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"strategy-opening-execution|{order.Format()}|{workflow:N}"))[..16]);
}
