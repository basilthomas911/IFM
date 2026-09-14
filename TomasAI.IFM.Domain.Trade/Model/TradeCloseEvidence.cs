using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>Validates that closing fill evidence exactly offsets an established trade.</summary>
public static class TradeCloseEvidence
{
    /// <summary>Returns true when every established leg is closed exactly once as a balanced strategy.</summary>
    public static bool IsExact(EstablishedTradeDefinition trade, ExecutionFillEvidence[] fills, DateTime closedAtUtc)
    {
        if (closedAtUtc.Kind != DateTimeKind.Utc || fills.Length == 0 || trade.Legs.Length == 0)
            return false;
        var attemptIds = fills.Select(static fill => fill.ExecutionAttemptId).Distinct().ToArray();
        if (attemptIds.Length != 1 || attemptIds[0] == Guid.Empty)
            return false;
        foreach (var fill in fills)
        {
            if (fill.ExecutionFillId == Guid.Empty || fill.FilledAtUtc.Kind != DateTimeKind.Utc ||
                fill.SignedQuantity == 0 || fill.Price <= 0)
                return false;
            var leg = trade.Legs.SingleOrDefault(candidate => candidate.TradeLegId == fill.TradeLegId);
            if (leg is null || !string.Equals(leg.ContractId, fill.ContractId, StringComparison.Ordinal) ||
                Math.Sign(fill.SignedQuantity) == Math.Sign(leg.SignedQuantity))
                return false;
        }
        return trade.Legs.All(leg => fills.Where(fill => fill.TradeLegId == leg.TradeLegId)
            .Sum(static fill => fill.SignedQuantity) == -leg.SignedQuantity);
    }
}
