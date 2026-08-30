using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Models.Portfolio;

public sealed record PortfolioNavigationModel(PortfolioReadModel Portfolio, IReadOnlyList<FundMandateReadModel> Funds)
{
    public IReadOnlyList<FundMandateReadModel> OrderedFunds => [.. Funds.OrderBy(x => x.FundCode, StringComparer.Ordinal)];
}
