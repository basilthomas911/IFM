using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one established trade created from a component of a Trade Order.</summary>
[MessagePackObject]
public readonly record struct TradeEntityId(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId,
    [property: Key(3)] int TradeId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{PortfolioId}.{FundId}.{OrderId}.{TradeId}");
    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0 && TradeId > 0;

    public static TradeEntityId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string[] parts = value.Split('.', StringSplitOptions.None);
        if (parts.Length != 4
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int portfolioId)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int fundId)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int orderId)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int tradeId))
            throw new FormatException($"Invalid TradeEntityId '{value}'. Expected PortfolioId.FundId.OrderId.TradeId.");

        var result = new TradeEntityId(portfolioId, fundId, orderId, tradeId);
        return result.IsValid
            ? result
            : throw new FormatException($"Invalid TradeEntityId '{value}'. All identity values must be positive.");
    }
}
