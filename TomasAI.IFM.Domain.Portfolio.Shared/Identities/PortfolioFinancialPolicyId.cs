using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Identities;

/// <summary>Identifies one Portfolio-owned financial policy aggregate.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFinancialPolicyId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int PolicyId { get; init; }

    public PortfolioFinancialPolicyId() { }
    public PortfolioFinancialPolicyId(int portfolioId, int policyId) =>
        (PortfolioId, PolicyId) = (portfolioId, policyId);
    public string Format() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{PortfolioId}.{PolicyId}");
    public IReadOnlyList<string> Validate() => PortfolioId > 0 && PolicyId > 0
        ? [] : ["PortfolioId and PolicyId must be greater than zero."];
    public override string ToString() => Format();
}
