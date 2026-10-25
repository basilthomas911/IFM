using TomasAI.IFM.Shared.MarketData.Diagnostics;
using System.Globalization;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Stores current-day Databento contract-ID/instrument-ID pairs in both directions.
/// </summary>
public interface IDatabentoContractMappingCache
{
    bool TryGetInstrumentId(
        string dataset,
        string contractId,
        out uint instrumentId);

    bool TryGetContractId(
        string dataset,
        uint instrumentId,
        out string? contractId);

    void SetMapping(
        string dataset,
        string contractId,
        uint instrumentId,
        ContractMappingDirection sourceDirection);

    void ClearMapping(string dataset, string contractId);

    void ClearMapping(string dataset, uint instrumentId);

    void ClearCurrentMappings(string dataset);
}
