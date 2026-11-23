using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed class WorkflowCompositionInputCacheTests
{
    [Fact]
    public async Task Ready_exact_futures_capture_uses_only_hot_prices_and_keeps_original_quote_times()
    {
        var at = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
        var command = await CompositionFixture.Command(atUtc: at);
        var market = Substitute.For<IMarketDataApi>();
        var store = Substitute.For<ICompositionRoutePlanStore>();
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = new DatasetWorkerAdmission("GLBX.MDP3", DateOnly.FromDateTime(at), Guid.NewGuid(), Guid.NewGuid(), 1);
        admissions.Admit(identity);
        var definition = new FuturesContractV3ReadModel { ContractId = "ESZ6", Symbol = "ES", LocalSymbol = "ESZ6", Exchange = "CME", Currency = "USD",
            Dataset = "GLBX.MDP3", SchemaVersion = 1, LastTradeDate = DateOnly.FromDateTime(at.AddDays(90)),
            LastTradingUtc = new(at.AddDays(90)), MultiplierValue = 50, TickSize = .25m, DefinitionDigest = new('a', 64) };
        market.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>()).Returns(call => { call[1] = definition; return true; });
        var observed = new DateTimeOffset(at.AddMilliseconds(-200));
        var quote = new FuturesMarketQuoteSnapshot(5000m, 10, 5000.25m, 10, 1, 1, 45, observed, observed.AddMilliseconds(10));
        var price = new FuturesMarketPriceSnapshot("ESZ6", 123, 1,
            TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures, identity.ValueDate, quote, null);
        market.TryGetLastTickPrice("ESZ6", out Arg.Any<FuturesMarketPriceSnapshot>()).Returns(call => { call[1] = price; return true; });
        market.GetFuturesMarketHealth("ESZ6").Returns(new FuturesMarketHealthSnapshot(true, true,
            $"{identity.Dataset}|{identity.WorkerInstanceId:N}|{identity.GenerationId:N}|1", identity.ValueDate, new(at), 45));
        store.SaveAsync(Arg.Any<CompositionRoutePlan>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var cache = new WorkflowCompositionInputCache(market, store, admissions, new FixedClock(at));
        await cache.PrepareAsync(command.SelectionBinding, default);
        await store.Received(1).SaveAsync(Arg.Any<CompositionRoutePlan>(), Arg.Any<CancellationToken>());
        store.ClearReceivedCalls(); market.ClearReceivedCalls();
        var view = command.WorkflowView with { DecisionRuntimeVersion = 1 };
        var result = await cache.CaptureAsync(view, identity.ValueDate);
        result.Failure.Should().BeNull();
        var captured = result.Preparation!.Snapshot.Instruments.Should().ContainSingle().Subject.Instrument.Quote!;
        captured.EventAtUtc.Should().Be(observed);
        captured.ReceivedAtUtc.Should().Be(quote.ReceiveTimestamp);
        captured.GenerationId.Should().Be(identity.GenerationId);
        (await cache.CaptureAsync(view, identity.ValueDate)).Should().BeSameAs(result);
        store.ReceivedCalls().Should().BeEmpty();
        market.ReceivedCalls().Should().OnlyContain(call => call.GetMethodInfo().Name == "TryGetLastTickPrice" || call.GetMethodInfo().Name == "GetFuturesMarketHealth");
        market.ReceivedCalls().Should().HaveCount(2);
    }

    [Fact]
    public async Task Missing_inputs_do_not_load_wait_or_recapture_after_configuration_is_published()
    {
        var at = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
        var command = await CompositionFixture.Command(atUtc: at);
        var market = Substitute.For<IMarketDataApi>();
        var store = Substitute.For<ICompositionRoutePlanStore>();
        var cache = new WorkflowCompositionInputCache(market, store, new DatasetWorkerAdmissionRegistry(), new FixedClock(at));
        var view = command.WorkflowView with { DecisionRuntimeVersion = 1 };
        var first = await cache.CaptureAsync(view, DateOnly.FromDateTime(at));
        first.Failure!.Code.Should().Be("OC.CONFIG.CACHE_NOT_READY");
        market.ReceivedCalls().Should().BeEmpty();
        await FluentActions.Awaiting(() => cache.PrepareAsync(command.SelectionBinding, default))
            .Should().ThrowAsync<CompositionMarketSourceException>().WithMessage("OC.INPUT.FUTURE_METADATA_NOT_READY");
        market.ClearReceivedCalls();
        var replay = await cache.CaptureAsync(view, DateOnly.FromDateTime(at));
        replay.Should().BeSameAs(first);
        replay.Preparation.Should().BeNull();
        market.ReceivedCalls().Should().BeEmpty();
        store.ReceivedCalls().Should().BeEmpty();
        // A later workflow can observe the prepared policy, but an unqualified universe still cannot fall back to I/O.
        var later = await cache.CaptureAsync(view with { WorkflowId = new(Guid.NewGuid()) }, DateOnly.FromDateTime(at));
        later.Failure!.Code.Should().Be("OC.CONFIG.CACHE_NOT_READY");
        market.ReceivedCalls().Should().BeEmpty();
        store.ReceivedCalls().Should().BeEmpty();
    }

    sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
