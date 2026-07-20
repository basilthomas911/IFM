using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;

/// <summary>
/// Defines read-only operations for retrieving futures and futures option contract data from the securities database.
/// </summary>
/// <remarks>This interface provides asynchronous methods to query currently traded and historical futures
/// contracts, as well as futures option contracts, by symbol, contract identifier, or in bulk. Implementations are
/// expected to provide efficient, read-only access to contract information for use in trading, analytics, or reporting
/// scenarios.</remarks>
public interface ISecuritiesDbReadContext
{
    Task<FuturesContractV2ReadModel?> GetCurrentlyTradedFuturesContractAsync(string symbol);
    Task<ICollection<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractsAsync(string symbol);
    Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(string contractId);
    Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(FuturesContractId contractId);
    Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsAsync();
    Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsBySymbolAsync(string symbol);
    Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol);
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync();
}
