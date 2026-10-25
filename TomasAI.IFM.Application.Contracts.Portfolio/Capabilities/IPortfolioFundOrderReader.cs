using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.Application.Contracts.Portfolio;
/// <summary>Reads an authoritative order without exposing Portfolio's aggregate implementation.</summary>
public interface IPortfolioFundOrderReader
{
    Task<FundOrderProjectionReadModel> ReadOrderAsync(PortfolioFundId fund, int orderId, CancellationToken cancellationToken = default);
}
