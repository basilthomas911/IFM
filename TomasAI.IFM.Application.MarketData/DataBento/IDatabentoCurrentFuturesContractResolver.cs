using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed record ResolvedCurrentFuturesContract(
    FuturesContractV2ReadModel Contract,
    DateOnly NextRolloverDate);

public interface IDatabentoCurrentFuturesContractResolver
{
    Task<ResolvedCurrentFuturesContract> ResolveAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);
}
