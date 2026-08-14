using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataApiQueryAndPriceContractTests
{
    [Fact]
    public async Task Normalized_last_price_delegates_without_requiring_an_active_stream()
    {
        var context = new MarketDataApiTestContext();
        context.Api.TryGetLastTickPrice(MarketDataApiTestContext.FutureId, out _)
            .Should().BeFalse();
        await context.StartAsync();
        var expected = new FuturesMarketPriceSnapshot(
            MarketDataApiTestContext.FutureId,
            42,
            7,
            AssetTypeId.Futures,
            MarketDataApiTestContext.ValueDate,
            null,
            new FuturesMarketTradeSnapshot(
                6500.25m,
                2,
                10,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));
        context.Epoch.LastMarketPrice = expected;
        context.Api.IsTickDataStreamActive(MarketDataApiTestContext.FutureId)
            .Should().BeFalse();
        context.Api.TryGetLastTickPrice(
                MarketDataApiTestContext.FutureId,
                out var actual)
            .Should().BeTrue();

        actual.Should().Be(expected);
        context.Api.TryGetLastTickPrice("MISSING", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Stream_activity_and_hot_cache_have_independent_lifecycles()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.LastMarketPrice = new FuturesMarketPriceSnapshot(
            MarketDataApiTestContext.FutureId,
            42,
            7,
            AssetTypeId.Futures,
            MarketDataApiTestContext.ValueDate,
            null,
            null);
        var owner = new TickerStreamOwner("Spread", "S1", "long");

        (await context.Api.StartStreamingFuturesTickDataAsync(
            MarketDataApiTestContext.FutureId,
            owner)).Should().BeTrue();
        context.Api.IsTickDataStreamActive(MarketDataApiTestContext.FutureId)
            .Should().BeTrue();

        (await context.Api.StopStreamingFuturesTickDataAsync(
            MarketDataApiTestContext.FutureId,
            owner)).Should().BeTrue();
        context.Api.IsTickDataStreamActive(MarketDataApiTestContext.FutureId)
            .Should().BeFalse();
        context.Api.TryGetLastTickPrice(MarketDataApiTestContext.FutureId, out _)
            .Should().BeTrue();
    }

    [Fact]
    public async Task OperationsRequireRunningEpoch()
    {
        var context = new MarketDataApiTestContext();

        var action = () => context.Api.GetFuturesContractAsync(MarketDataApiTestContext.FutureId);

        await action.Should().ThrowAsync<MarketDataApiNotRunningException>();
    }

    [Fact]
    public async Task SingleConfirmedMissReturnsNull()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var result = await context.Api.GetFuturesContractAsync("UNKNOWN-FUTURE");

        result.Should().BeNull();
    }

    [Fact]
    public async Task EmptyBatchReturnsEmptyWithoutProviderQuery()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var initialQueryCount = context.Catalog.ProviderQueryCount;

        var result = await context.Api.GetFuturesContractsAsync([]);

        result.Should().BeEmpty();
        context.Catalog.ProviderQueryCount.Should().Be(initialQueryCount);
    }

    [Fact]
    public async Task BatchPreservesInputOrderAndDuplicates()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var result = await context.Api.GetFuturesContractsAsync(
            [
                MarketDataApiTestContext.SecondFutureId,
                MarketDataApiTestContext.FutureId,
                MarketDataApiTestContext.SecondFutureId
            ]);

        result.Select(contract => contract.ContractId).Should().Equal(
            MarketDataApiTestContext.SecondFutureId,
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.SecondFutureId);
    }

    [Fact]
    public async Task BatchMissFailsWholeCallWithTypedException()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var action = () => context.Api.GetFuturesContractsAsync(
            [MarketDataApiTestContext.FutureId, "MISSING"]);

        var exception = await action.Should().ThrowAsync<MarketDataBatchResolutionException>();
        exception.Which.UnresolvedContractIds.Should().Equal("MISSING");
    }

    [Fact]
    public async Task WrongContractKindThrowsTypedException()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var action = () => context.Api.GetFuturesContractAsync(MarketDataApiTestContext.CallId);

        var exception = await action.Should().ThrowAsync<MarketDataContractKindMismatchException>();
        exception.Which.ContractId.Should().Be(MarketDataApiTestContext.CallId);
        exception.Which.ExpectedKind.Should().Be("futures");
    }

    [Fact]
    public async Task ChainDiscoveryReturnsStableStrikeTypeContractOrder()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var result = await context.Api.GetFuturesOptionChainContractsAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity);

        result.Select(option => option.ContractId).Should().Equal(
            MarketDataApiTestContext.CallId,
            MarketDataApiTestContext.PutId,
            MarketDataApiTestContext.SecondCallId);
    }

    [Fact]
    public async Task FuturesPriceUsesExactFreshLastTrade()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.GetFuturesReader(MarketDataApiTestContext.FutureId)
            .SetTrade(context.FreshFutureTrade(6500.125m));

        var price = await context.Api.GetFuturesPriceAsync(MarketDataApiTestContext.FutureId);

        price.Should().Be(6500.125m);
    }

    [Fact]
    public async Task MissingOrStaleFuturesTradeThrowsTypedUnavailableException()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var missingAction = () =>
            context.Api.GetFuturesPriceAsync(MarketDataApiTestContext.FutureId);
        await missingAction.Should().ThrowAsync<FuturesLastPriceUnavailableException>();

        context.Epoch.GetFuturesReader(MarketDataApiTestContext.FutureId).SetTrade(
            context.FreshFutureTrade() with
            {
                EventTimestamp = MarketDataApiTestContext.Now.AddSeconds(-3)
            });
        var staleAction = () =>
            context.Api.GetFuturesPriceAsync(MarketDataApiTestContext.FutureId);
        await staleAction.Should().ThrowAsync<FuturesLastPriceUnavailableException>();
    }

    [Fact]
    public async Task OptionPriceUsesMidpointAndMissingOneSidedOrStaleQuoteReturnsNull()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var reader = context.Epoch.GetOptionReader(MarketDataApiTestContext.CallId);

        (await context.Api.GetFuturesOptionPriceAsync(MarketDataApiTestContext.CallId))
            .Should().BeNull();

        reader.SetQuote(context.OptionQuote(10m, 12m));
        (await context.Api.GetFuturesOptionPriceAsync(MarketDataApiTestContext.CallId))
            .Should().Be(11m);

        reader.SetQuote(context.OptionQuote(10m, null));
        (await context.Api.GetFuturesOptionPriceAsync(MarketDataApiTestContext.CallId))
            .Should().BeNull();

        reader.SetQuote(context.OptionQuote(
            10m,
            12m,
            MarketDataApiTestContext.Now.AddSeconds(-3)));
        (await context.Api.GetFuturesOptionPriceAsync(MarketDataApiTestContext.CallId))
            .Should().BeNull();
    }

    [Fact]
    public async Task CrossedOptionQuoteThrowsTypedException()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.GetOptionReader(MarketDataApiTestContext.CallId)
            .SetQuote(context.OptionQuote(12m, 10m));

        var action = () => context.Api.GetFuturesOptionPriceAsync(MarketDataApiTestContext.CallId);

        await action.Should().ThrowAsync<InvalidFuturesOptionQuoteException>();
    }

    [Fact]
    public async Task ReaderIsStableWithinEpochAndInvalidAfterStop()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var reader = context.Api.GetFuturesLastPriceReader(MarketDataApiTestContext.FutureId);
        var sameReader = context.Api.GetFuturesLastPriceReader(MarketDataApiTestContext.FutureId);
        context.Epoch.GetFuturesReader(MarketDataApiTestContext.FutureId)
            .SetTrade(context.FreshFutureTrade());

        sameReader.Should().BeSameAs(reader);
        reader.TryGetLastTrade(out _).Should().BeTrue();

        await context.Api.StopAsync(MarketDataApiTestContext.ValueDate);

        reader.TryGetLastTrade(out _).Should().BeFalse();
        reader.TryGetLastQuote(out _).Should().BeFalse();
    }

    [Fact]
    public async Task RawOptionTickDoesNotClaimAnEnrichedSnapshot()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var writer = context.Epoch.GetOptionReader(MarketDataApiTestContext.CallId);
        var reader = context.Api.GetFuturesOptionLastPriceReader(
            MarketDataApiTestContext.CallId);
        writer.SetQuote(context.OptionQuote(10m, 12m));

        reader.TryGetLastQuote(out _).Should().BeTrue();
        reader.TryGetLastQuoteWithGreeks(out _).Should().BeFalse();
    }

    [Fact]
    public async Task EnrichedQuoteIsReturnedAtomicallyForItsExactTick()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var writer = context.Epoch.GetOptionReader(MarketDataApiTestContext.CallId);
        var reader = context.Api.GetFuturesOptionLastPriceReader(
            MarketDataApiTestContext.CallId);
        var quote = context.OptionQuote(10m, 12m);
        var greeks = context.ValidGreeks(quote.SourceSequence);
        writer.SetQuoteWithGreeks(new(quote, greeks));

        reader.TryGetLastQuoteWithGreeks(out var enriched).Should().BeTrue();
        enriched.Tick.Should().Be(quote);
        enriched.Greeks.Should().Be(greeks);
        enriched.Greeks.OptionPriceSourceSequence.Should()
            .Be(enriched.Tick.SourceSequence);
    }

    [Fact]
    public async Task AvailableButInvalidGreeksRemainObservableWithoutSentinels()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var writer = context.Epoch.GetOptionReader(MarketDataApiTestContext.CallId);
        var reader = context.Api.GetFuturesOptionLastPriceReader(
            MarketDataApiTestContext.CallId);
        var quote = context.OptionQuote(10m, 12m);
        var failedGreeks = context.ValidGreeks(quote.SourceSequence) with
        {
            IsValid = false,
            FailureReason = OptionGreeksFailureReason.SolverDidNotConverge,
            ImpliedVolatility = null,
            TheoreticalPrice = null,
            Delta = null,
            Gamma = null,
            Vega = null,
            Theta = null,
            Rho = null
        };
        writer.SetQuoteWithGreeks(new(quote, failedGreeks));

        reader.TryGetLastQuoteWithGreeks(out var enriched).Should().BeTrue();
        enriched.Greeks.IsValid.Should().BeFalse();
        enriched.Greeks.FailureReason.Should()
            .Be(OptionGreeksFailureReason.SolverDidNotConverge);
        enriched.Greeks.Delta.Should().BeNull();
    }

    [Fact]
    public async Task TradeCarriesLatestQuoteDerivedGreeksAndInvalidatesOnEpochStop()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var writer = context.Epoch.GetOptionReader(MarketDataApiTestContext.CallId);
        var reader = context.Api.GetFuturesOptionLastPriceReader(
            MarketDataApiTestContext.CallId);
        var trade = context.FreshOptionTrade(sourceSequence: 103);
        var quoteDerivedGreeks = context.ValidGreeks(optionPriceSourceSequence: 102);
        writer.SetTradeWithGreeks(new(trade, quoteDerivedGreeks));

        reader.TryGetLastTradeWithGreeks(out var enriched).Should().BeTrue();
        enriched.Tick.SourceSequence.Should().Be(103);
        enriched.Greeks.OptionPriceSourceSequence.Should().Be(102);
        enriched.Greeks.PriceSource.Should().Be(OptionGreeksPriceSource.QuoteMidpoint);

        await context.Api.StopAsync(MarketDataApiTestContext.ValueDate);

        reader.TryGetLastTradeWithGreeks(out _).Should().BeFalse();
        reader.TryGetLastQuoteWithGreeks(out _).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentEnrichedReadsNeverTearTickFromGreeks()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        var writer = context.Epoch.GetOptionReader(MarketDataApiTestContext.CallId);
        var reader = context.Api.GetFuturesOptionLastPriceReader(
            MarketDataApiTestContext.CallId);
        var completed = 0;
        var mismatch = 0;

        var writeTask = Task.Run(() =>
        {
            for (var sequence = 1L; sequence <= 10_000; sequence++)
            {
                var quote = context.OptionQuote(10m, 12m) with
                {
                    SourceSequence = sequence
                };
                writer.SetQuoteWithGreeks(new(
                    quote,
                    context.ValidGreeks(sequence)));
            }
            Volatile.Write(ref completed, 1);
        });
        var readTask = Task.Run(() =>
        {
            while (Volatile.Read(ref completed) == 0)
            {
                if (reader.TryGetLastQuoteWithGreeks(out var enriched)
                    && enriched.Tick.SourceSequence
                        != enriched.Greeks.OptionPriceSourceSequence)
                {
                    Volatile.Write(ref mismatch, 1);
                    return;
                }
            }
        });

        await Task.WhenAll(writeTask, readTask);

        Volatile.Read(ref mismatch).Should().Be(0);
    }
}
