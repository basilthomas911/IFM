using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Identities;

/// <summary>Identifies one Fund mandate within its Portfolio.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }

    public PortfolioFundId() { }

    public PortfolioFundId(int portfolioId, int fundId) =>
        (PortfolioId, FundId) = (portfolioId, fundId);

    public string Format() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{PortfolioId}.{FundId}");

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioFundId.PortfolioId must be greater than zero.");
        if (FundId <= 0) errors.Add("PortfolioFundId.FundId must be greater than zero.");
        return errors;
    }

    public override string ToString() => Format();
}
