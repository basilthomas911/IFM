using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

/// <summary>
/// Defines the Fund query contract for direct, in-process use by domain actors.
/// </summary>
/// <remarks>
/// Implementations must not use HTTP, NATS, or another actor-messaging transport.
/// </remarks>
public interface IActorFundQueryApi : IFundQueryApi
{
    Task<ServiceResult<FundMaxProfitGeneratedReadModel>> GetFundMaxProfitGeneratedAsync(
        int fundId,
        DateOnly tradeDate);
}
