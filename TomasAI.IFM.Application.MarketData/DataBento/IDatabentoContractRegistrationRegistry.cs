using System.Collections;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public interface IDatabentoContractRegistrationRegistry :
    IReadOnlyList<DatabentoContractRegistration>
{
    IReadOnlyList<DatabentoContractRegistration> Snapshot();

    bool TryGetOnTheRunFuturesContract(
        string symbol,
        out FuturesContractV3ReadModel contract);

    bool TryGetFuturesTermStructureContracts(
        string symbol,
        out FuturesTermStructureContracts contracts);

    void ReplaceFuturesRolloverSet(
        string symbol,
        IReadOnlyCollection<FuturesContractV3ReadModel> contracts);
}
