using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;

public interface ISecuritiesDbWriteContext
{
    Task DeleteFuturesContractAsync(string contractId);
    Task DeleteFuturesContractAsync(FuturesContractId contractId);
    Task DeleteCurrentlyTradedFuturesContractAsync(string symbol);
    Task DeleteFuturesOptionContractAsync(string contractId);
    Task InsertFuturesContractAsync(FuturesContractV2ReadModel futuresContract);
    Task InsertFuturesContractsAsync(ICollection<FuturesContractV2ReadModel> futuresContracts);
    Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract);
    Task InsertFuturesOptionContractsAsync(ICollection<FuturesOptionContractReadModel> futuresOptionContracts);
    Task UpdateFuturesContractAsync(FuturesContractId originalContractId, FuturesContractV2ReadModel futuresContract);
    Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel futuresOptionContract);
}
