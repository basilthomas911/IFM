using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Identities;

/// <summary>Identifies one planned FundOrder within its Portfolio and Fund.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundOrderId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }

    public PortfolioFundOrderId() { }

    public PortfolioFundOrderId(int portfolioId, int fundId, int orderId) =>
        (PortfolioId, FundId, OrderId) = (portfolioId, fundId, orderId);

    public string Format() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{PortfolioId}.{FundId}.{OrderId}");

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioFundOrderId.PortfolioId must be greater than zero.");
        if (FundId <= 0) errors.Add("PortfolioFundOrderId.FundId must be greater than zero.");
        if (OrderId <= 0) errors.Add("PortfolioFundOrderId.OrderId must be greater than zero.");
        return errors;
    }

    public override string ToString() => Format();
}
