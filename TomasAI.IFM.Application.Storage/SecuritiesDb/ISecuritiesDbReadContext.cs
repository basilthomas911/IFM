using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>
/// Defines read-only operations for retrieving futures and futures option contract data from the securities database.
/// </summary>
/// <remarks>This interface provides asynchronous methods to query currently traded and historical futures
/// contracts, as well as futures option contracts, by symbol, contract identifier, or in bulk. Implementations are
/// expected to provide efficient, read-only access to contract information for use in trading, analytics, or reporting
/// scenarios.</remarks>
public interface ISecuritiesDbReadContext
{
    Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol);
    Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol, CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol);
    Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol, CancellationToken cancellationToken);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId, CancellationToken cancellationToken);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId contractId);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId contractId, CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync();
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync(CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol, CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol, CancellationToken cancellationToken);
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId);
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId, CancellationToken cancellationToken);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(ICollection<string> contractIds);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(ICollection<string> contractIds, CancellationToken cancellationToken);
    Task<FuturesOptionContractPageReadModel> GetFuturesOptionContractsPageAsync(
        TomasAI.IFM.Domain.MarketData.Shared.QueryParameters.GetFuturesOptionContractsPageParameter request,
        CancellationToken cancellationToken = default);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol, CancellationToken cancellationToken);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync();
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(CancellationToken cancellationToken);
}
