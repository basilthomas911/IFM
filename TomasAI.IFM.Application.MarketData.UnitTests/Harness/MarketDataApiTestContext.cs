using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Application.MarketData.Databento;

namespace TomasAI.IFM.Application.MarketData.UnitTests.Harness;

internal sealed class MarketDataApiTestContext
{
    internal static readonly DateOnly ValueDate = new(2026, 8, 10);
    internal static readonly DateOnly NextValueDate = new(2026, 8, 11);
    internal static readonly DateOnly OptionMaturity = new(2026, 9, 18);
    internal static readonly DateTimeOffset Now =
        new(2026, 8, 10, 20, 0, 0, TimeSpan.Zero);

    internal const string FutureId = "ES-202609";
    internal const string SecondFutureId = "NQ-202609";
    internal const string CallId = "ES20260918C6500";
    internal const string PutId = "ES20260918P6500";
    internal const string SecondCallId = "ES20260918C6550";

    internal MarketDataApiTestContext()
    {
        Catalog.Futures.Add(FutureId, Future(FutureId, "ES", "ESU6"));
        Catalog.Futures.Add(SecondFutureId, Future(SecondFutureId, "NQ", "NQU6"));
        Catalog.Options.Add(CallId, Option(CallId, 6500, "Call"));
        Catalog.Options.Add(PutId, Option(PutId, 6500, "Put"));
        Catalog.Options.Add(SecondCallId, Option(SecondCallId, 6550, "Call"));
        Catalog.OptionUnderlyings.Add(CallId, FutureId);
        Catalog.OptionUnderlyings.Add(PutId, FutureId);
        Catalog.OptionUnderlyings.Add(SecondCallId, FutureId);

        EpochFactory = new FakeMarketDataEpochFactory(Catalog);
        Api = new DatabentoMarketDataApi(
            EpochFactory,
            new DatabentoMarketDataApiOptions
            {
                MaximumLastPriceAge = TimeSpan.FromSeconds(2)
            },
            new FixedTimeProvider(Now));
    }

    internal FakeMarketDataCatalog Catalog { get; } = new();
    internal FakeMarketDataEpochFactory EpochFactory { get; }
    internal DatabentoMarketDataApi Api { get; }
    internal FakeMarketDataEpoch Epoch =>
        Api.ActiveValueDate is not null
            ? EpochFactory.Epochs[^1]
            : throw new InvalidOperationException("The test epoch is not running.");

    internal async Task StartAsync()
    {
        await Api.StartAsync(ValueDate);
        Epoch.TickAggregation.ConfiguredTickers.Add(FutureId);
        Epoch.TickAggregation.RunningTickers.Add(FutureId);
    }

    internal LastTradeTickSnapshot FreshFutureTrade(decimal price = 6500.25m) => new(
        FutureId,
        ValueDate,
        price,
        3,
        101,
        Now.AddMilliseconds(-10),
        Now.AddMilliseconds(-5));

    internal LastQuoteTickSnapshot FreshFutureQuote(
        decimal? bid = 6500m,
        decimal? ask = 6500.5m,
        DateTimeOffset? eventTimestamp = null) => new(
        FutureId,
        ValueDate,
        bid,
        bid.HasValue ? 4U : 0U,
        bid.HasValue ? 1U : 0U,
        ask,
        ask.HasValue ? 5U : 0U,
        ask.HasValue ? 1U : 0U,
        102,
        eventTimestamp ?? Now.AddMilliseconds(-10),
        Now.AddMilliseconds(-5));

    internal LastQuoteTickSnapshot OptionQuote(
        decimal? bid,
        decimal? ask,
        DateTimeOffset? eventTimestamp = null,
        string contractId = CallId,
        DateOnly? valueDate = null) => new(
            contractId,
            valueDate ?? ValueDate,
            bid,
            bid.HasValue ? 4U : 0U,
            bid.HasValue ? 1U : 0U,
            ask,
            ask.HasValue ? 5U : 0U,
            ask.HasValue ? 1U : 0U,
            102,
            eventTimestamp ?? Now.AddMilliseconds(-10),
            Now.AddMilliseconds(-5));

    internal LastTradeTickSnapshot FreshOptionTrade(
        decimal price = 11m,
        long sourceSequence = 103) => new(
            CallId,
            ValueDate,
            price,
            2,
            sourceSequence,
            Now.AddMilliseconds(-8),
            Now.AddMilliseconds(-4));

    internal OptionGreeksSnapshot ValidGreeks(
        long optionPriceSourceSequence = 102) => new(
            true,
            false,
            OptionGreeksFailureReason.None,
            OptionGreeksPriceSource.QuoteMidpoint,
            FutureId,
            6500.25m,
            11m,
            0.04,
            39d / 365d,
            0.21,
            11.01,
            0.51,
            0.0012,
            18.4,
            -2.1,
            -4.3,
            4,
            101,
            optionPriceSourceSequence,
            Now.AddMilliseconds(-12),
            Now.AddMilliseconds(-10),
            Now.AddMilliseconds(-9));

    private static FuturesContractV2ReadModel Future(
        string contractId,
        string symbol,
        string localSymbol) => new(
            contractId,
            $"{symbol} September 2026",
            symbol,
            localSymbol,
            "FUT",
            "USD",
            "CME",
            "50",
            new DateOnly(2026, 9, 18),
            true);

    private static FuturesOptionContractReadModel Option(
        string contractId,
        double strike,
        string optionType) => new(
            contractId,
            $"ES {OptionMaturity:yyyy-MM-dd} {optionType} {strike}",
            "ES",
            contractId,
            "FOP",
            "USD",
            "CME",
            "50",
            OptionMaturity,
            strike,
            optionType);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
