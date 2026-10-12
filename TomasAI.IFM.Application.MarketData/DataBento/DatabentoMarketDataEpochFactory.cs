using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed class DatabentoMarketDataEpochFactory : IDatabentoMarketDataEpochFactory
{
    private readonly IDatabentoFeedFactory _feeds;
    private readonly ITickAggregationEventPublisher _publisher;
    private readonly DatabentoMarketDataRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ITickLiveEventPublisher _livePublisher;
    private readonly DatabentoTerminalFaultSignal? _terminalFaultSignal;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly Pricing.IOptionTradeEvidenceWriter? _tradeEvidence;

    public DatabentoMarketDataEpochFactory(
        IDatabentoFeedFactory feeds,
        ITickAggregationEventPublisher publisher,
        DatabentoMarketDataRuntimeOptions options,
        TimeProvider? timeProvider = null,
        ITickLiveEventPublisher? livePublisher = null,
        DatabentoTerminalFaultSignal? terminalFaultSignal = null,
        ILoggerFactory? loggerFactory = null, Pricing.IOptionTradeEvidenceWriter? tradeEvidence = null)
    {
        _feeds = feeds ?? throw new ArgumentNullException(nameof(feeds));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _livePublisher = livePublisher ?? new NullTickLiveEventPublisher();
        _terminalFaultSignal = terminalFaultSignal;
        _loggerFactory = loggerFactory;
        _tradeEvidence = tradeEvidence;
    }

    public IDatabentoMarketDataEpoch Create(DateOnly valueDate)
    {
        var contracts = _options.Contracts is IDatabentoContractRegistrationRegistry registry
            ? registry.Snapshot()
            : _options.Contracts.ToArray();
        var snapshot = _options with { Contracts = contracts };
        return
        new DatabentoMarketDataEpoch(
            valueDate, _feeds, _publisher, snapshot, _timeProvider, _livePublisher,
            _terminalFaultSignal is null ? null : detail => _terminalFaultSignal.Notify(detail),
            _loggerFactory, _tradeEvidence);
    }
}
