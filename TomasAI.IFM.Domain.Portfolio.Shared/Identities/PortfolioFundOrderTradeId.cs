using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Identities;

/// <summary>Identifies one planned FundOrderTrade.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundOrderTradeId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public int TradeId { get; init; }

    public PortfolioFundOrderTradeId() { }

    public PortfolioFundOrderTradeId(int portfolioId, int fundId, int orderId, int tradeId) =>
        (PortfolioId, FundId, OrderId, TradeId) = (portfolioId, fundId, orderId, tradeId);

    public string Format() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{PortfolioId}.{FundId}.{OrderId}.{TradeId}");

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioFundOrderTradeId.PortfolioId must be greater than zero.");
        if (FundId <= 0) errors.Add("PortfolioFundOrderTradeId.FundId must be greater than zero.");
        if (OrderId <= 0) errors.Add("PortfolioFundOrderTradeId.OrderId must be greater than zero.");
        if (TradeId <= 0) errors.Add("PortfolioFundOrderTradeId.TradeId must be greater than zero.");
        return errors;
    }

    public override string ToString() => Format();
}
