using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>Validates that closing fill evidence exactly offsets an established trade.</summary>
public static class TradeCloseEvidence
{
    /// <summary>Accumulates actual closing fills without consuming the same evidence twice.</summary>
    public static bool TryApply(EstablishedTradeDefinition trade, ExecutionFillEvidence[] fills, DateTime at,
        out EstablishedTradeDefinition updated)
    {
        updated=trade;
        if(at.Kind!=DateTimeKind.Utc || fills.Length==0 || trade.Legs.Length==0 ||
            fills.Select(x=>x.ExecutionAttemptId).Distinct().Count()!=1 ||
            fills.Select(x=>x.ExecutionFillId).Distinct().Count()!=fills.Length) return false;
        var previousFills=trade.ClosingFills ?? [];
        var accumulated=previousFills.ToDictionary(x=>x.ExecutionFillId);
        foreach(var fill in fills)
        {
            if(fill.ExecutionFillId==Guid.Empty || fill.ExecutionAttemptId==Guid.Empty || fill.FilledAtUtc.Kind!=DateTimeKind.Utc ||
                fill.SignedQuantity==0 || fill.Price<=0 || string.IsNullOrWhiteSpace(fill.ExternalExecutionId)) return false;
            var candidates=trade.Legs.Where(x=>x.ContractId==fill.ContractId).ToArray();
            if(candidates.Length!=1) return false;
            var normalized=fill with { TradeLegId=candidates[0].TradeLegId };
            if(accumulated.TryGetValue(fill.ExecutionFillId,out var prior))
            {
                if(prior!=normalized) return false;
                continue;
            }
            if(accumulated.Values.Any(x=>x.ExternalExecutionId==fill.ExternalExecutionId)) return false;
            accumulated.Add(fill.ExecutionFillId,normalized);
        }
        var closed=true;
        foreach(var leg in trade.Legs)
        {
            var opening=trade.OriginalFills.Where(x=>x.TradeLegId==leg.TradeLegId).ToArray();
            if(opening.Length==0 || opening.Any(x=>x.ContractId!=leg.ContractId || x.SignedQuantity==0 ||
                Math.Sign(x.SignedQuantity)!=Math.Sign(leg.SignedQuantity))) return false;
            var quantity=opening.Sum(x=>(long)x.SignedQuantity);
            var closing=accumulated.Values.Where(x=>x.TradeLegId==leg.TradeLegId).ToArray();
            if(closing.Any(x=>Math.Sign(x.SignedQuantity)==Math.Sign(quantity))) return false;
            var consumed=closing.Sum(x=>(long)x.SignedQuantity);
            if(Math.Abs(consumed)>Math.Abs(quantity)) return false;
            closed &= quantity+consumed==0;
        }
        var changed=accumulated.Count!=previousFills.Length;
        updated=trade with { Status=closed?EstablishedTradeStatus.Closed:EstablishedTradeStatus.Open,
            ClosingFills=accumulated.Values.OrderBy(x=>x.ExecutionFillId).ToArray(),
            ClosedAtUtc=closed?accumulated.Values.Max(x=>x.FilledAtUtc):null,
            EvidenceRevision=changed?checked(trade.EvidenceRevision+1):trade.EvidenceRevision };
        return true;
    }

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
