using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento;

public readonly record struct DatabentoMarketDataEpochHealth(
    DateOnly ValueDate,
    bool Running,
    bool AggregationRunning,
    int ConfiguredContracts,
    int LastPriceSlots,
    bool LastPriceStoreActive,
    long SourceQuoteRecords,
    long SourceTradeRecords,
    long PublicationFailures,
    long ProcessingFailures = 0,
    IReadOnlyList<TickAggregationContractStatus>? ContractStatuses = null,
    IReadOnlyList<DatabentoDatasetFeedHealth>? DatasetFeedStatuses = null);
