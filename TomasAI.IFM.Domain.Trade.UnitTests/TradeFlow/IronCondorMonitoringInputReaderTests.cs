using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorMonitoringInputReaderTests
{
    [Fact]
    public async Task Hung_storage_read_does_not_block_ticks_or_create_parallel_refreshes()
    {
        var databases = Substitute.For<IDbContextFactory>();
        var trades = Substitute.For<ITradeDbContext>();
        databases.TradeDb.Returns(trades);
        var pending = new TaskCompletionSource<EstablishedTradeDefinition?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tradeId = new TradeEntityId(1, 2, 3, 4);
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        trades.GetEstablishedTradeAsync(tradeId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            readStarted.TrySetResult();
            return pending.Task;
        });
        var reader = new IronCondorMonitoringInputReader(databases, Substitute.For<ILogger>());
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(tradeId, TradeStrategyKind.IronCondor) };
        var timer = Stopwatch.StartNew();
        for (var index = 0; index < 10; index++) reader.Capture(position, new DateOnly(2026, 10, 7), DateTime.UtcNow).Should().BeNull();
        timer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _ = trades.Received(1).GetEstablishedTradeAsync(tradeId, Arg.Any<CancellationToken>());
        pending.TrySetResult(null);
    }

    [Theory]
    [InlineData(1, TradeType.ShortIronCondor)]
    [InlineData(-1, TradeType.LongIronCondor)]
    public void Strategy_direction_comes_from_leg_strikes_and_actions(int direction, TradeType expected)
        => IronCondorMonitoringInputReader.IdentifyTradeType(Legs(direction)).Should().Be(expected);

    [Fact]
    public void Unequal_leg_quantities_are_rejected()
    {
        var legs = Legs(1);
        legs[0] = legs[0] with { SignedQuantity = -2 };
        FluentActions.Invoking(() => IronCondorMonitoringInputReader.IdentifyTradeType(legs)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Daily_ema_capture_excludes_the_open_session_and_reuses_completed_session_prices()
    {
        var databases = Substitute.For<IDbContextFactory>();
        var trades = Substitute.For<ITradeDbContext>();
        var market = Substitute.For<TomasAI.IFM.Application.Storage.MarketDataDb.IMarketDataDbContext>();
        var securities = Substitute.For<TomasAI.IFM.Application.Storage.SecuritiesDb.ISecuritiesDbContext>();
        databases.TradeDb.Returns(trades); databases.MarketDataDb.Returns(market); databases.SecuritiesDb.Returns(securities);
        var tradeId = new TradeEntityId(1, 2, 3, 4);
        var date = new DateOnly(2026, 10, 7);
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var legs = Legs(1).Select((leg, index) => leg with { ContractId = $"option-{index}", Expiry = date.AddDays(10) }).ToArray();
        trades.GetEstablishedTradeAsync(tradeId, Arg.Any<CancellationToken>()).Returns(new EstablishedTradeDefinition
            { Id = tradeId, StrategyKind = TradeStrategyKind.IronCondor, Legs = legs, EstablishedAtUtc = now.AddDays(-1) });
        securities.GetFuturesOptionContractAsync(legs[0].ContractId, Arg.Any<CancellationToken>()).Returns(
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel { UnderlyingContractId = "ESZ6" });
        securities.GetFuturesContractAsync("ESZ6", Arg.Any<CancellationToken>()).Returns(
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel { ContractId = "ESZ6", Symbol = "ES" });
        trades.GetTradePlanForwardLossRatiosAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(
            Array.Empty<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanForwardLossRatioReadModel>());
        market.GetFuturesEodClosingPricesAsync("ESZ6", "ES", date.AddDays(-120), date.AddDays(-1), 60).Returns(
            Enumerable.Range(0, 7).Select(index => new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.FuturesEodClosingPriceReadModel
                { Symbol = "ES", ValueDate = date.AddDays(-index), ClosingPrice = index == 0 ? 99999 : (7-index)*10m }).ToArray());
        // A legacy stop/history query can hang; the current stop comes only from the function's latest source snapshot.
        var legacyStop = new TaskCompletionSource<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanStopLossLimitReadModel?>();
        trades.GetTradePlanStopLossLimitAsync(3, 4).Returns(legacyStop.Task);
        var reader = new IronCondorMonitoringInputReader(databases, Substitute.For<ILogger>());
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(tradeId, TradeStrategyKind.IronCondor) };
        TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanInputs? captured = null;
        var deadline = Stopwatch.StartNew();
        while ((captured = reader.Capture(position, date, now)) is null && deadline.Elapsed < TimeSpan.FromSeconds(3))
            await Task.Delay(10);
        captured.Should().NotBeNull();
        captured!.FiveDayXMA.Should().BeApproximately(40, 1e-12);
        _ = trades.DidNotReceive().GetTradePlanStopLossLimitAsync(Arg.Any<int>(), Arg.Any<int>());
        reader.Capture(position, date, now.AddSeconds(6));
        await Task.Delay(30);
        _ = market.Received(1).GetFuturesEodClosingPricesAsync("ESZ6", "ES", date.AddDays(-120), date.AddDays(-1), 60);
    }

    [Fact]
    public void Forward_delta_matches_exact_contracts_even_when_provider_scopes_arrive_in_a_different_order()
    {
        var now = DateTimeOffset.UtcNow;
        var legs = Legs(10).Select((leg, index) => leg with { ContractId = $"option-{index}" }).ToArray();
        double[] delta = [0.20, 0.09, -0.22, -0.07];
        var risk = legs.Select((leg, index) => new TomasAI.IFM.Application.MarketData.Pricing.MarketCompositionSnapshot(
            1, Guid.NewGuid(), leg.ContractId, "qualified", "Daily", Guid.NewGuid(), now, now.AddSeconds(5),
            [new(new(leg.ContractId, null, null, leg.Strike, leg.PutCall == 1, null),
                new(0.2, delta[index], 0.001, 0, 0, 0, 1, 0.1, "pricing-context"))], "digest")).Reverse().ToArray();
        IronCondorMonitoringInputReader.ForwardDelta(legs, risk).Should().BeApproximately(0.04, 1e-12);
        IronCondorMonitoringInputReader.ForwardDelta(legs, risk[..3]).Should().BeNull();
    }

    [Fact]
    public async Task A_blocked_distribution_commit_does_not_hold_current_prices_or_cause_parallel_history_writes()
    {
        var databases = Substitute.For<IDbContextFactory>();
        var trades = Substitute.For<ITradeDbContext>();
        var securities = Substitute.For<TomasAI.IFM.Application.Storage.SecuritiesDb.ISecuritiesDbContext>();
        databases.TradeDb.Returns(trades); databases.SecuritiesDb.Returns(securities);
        var (legs, risk) = IronCondorOptionCalculatorTests.Evidence(atUtc: DateTimeOffset.UtcNow);
        var now = risk[0].EvaluatedAtUtc.UtcDateTime;
        var date = DateOnly.FromDateTime(now);
        var tradeId = new TradeEntityId(1,2,3,4);
        trades.GetEstablishedTradeAsync(tradeId, Arg.Any<CancellationToken>()).Returns(new EstablishedTradeDefinition
            { Id = tradeId, StrategyKind = TradeStrategyKind.IronCondor, EstablishedAtUtc = now.AddDays(-7), Legs = legs });
        securities.GetFuturesOptionContractAsync(legs[0].ContractId, Arg.Any<CancellationToken>()).Returns(
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel { UnderlyingContractId = "ES-future" });
        trades.GetTradeLimitAsync(4, Arg.Any<CancellationToken>()).Returns(new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel
            { TradeId = 4, TradeType = TradeType.ShortIronCondor, MaxLoss = -2000, MaxProfit = 1300 });
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var historyCalls = 0; var pricingCalls = 0;
        var logger = Substitute.For<ILogger>();
        using var lifetime = new CancellationTokenSource();
        var reader = new IronCondorMonitoringInputReader(databases, logger,
            recordDistributions: (_, _) => { Interlocked.Increment(ref historyCalls); started.TrySetResult(); return pending.Task; },
            captureOptionRisk: (_, _, _) => { Interlocked.Increment(ref pricingCalls); return Task.FromResult(risk); }, stoppingToken: lifetime.Token);
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(tradeId, TradeStrategyKind.IronCondor),
            Legs = legs.Select(leg => new StrategyPositionLeg { ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity, PutCall = leg.PutCall }).ToArray() };
        TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanInputs? captured = null;
        var elapsed = Stopwatch.StartNew();
        while ((captured = reader.Capture(position, date, now))?.CalculatedSpreadPrices is null && elapsed.Elapsed < TimeSpan.FromSeconds(3))
            await Task.Delay(10);
        captured!.CalculatedSpreadPrices.Should().NotBeNull(string.Join("; ", logger.ReceivedCalls().SelectMany(call => call.GetArguments()).OfType<Exception>().Select(error => error.ToString())));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var first = captured.CalculatedSpreadPrices!.CalculatedAtUtc;
        elapsed.Restart();
        while ((captured = reader.Capture(position, date, now.AddSeconds(1.1)))?.CalculatedSpreadPrices?.CalculatedAtUtc <= first
            && elapsed.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
        captured!.CalculatedSpreadPrices!.CalculatedAtUtc.Should().BeAfter(first);
        pricingCalls.Should().Be(2); historyCalls.Should().Be(1);
        _ = trades.Received(1).GetEstablishedTradeAsync(tradeId, Arg.Any<CancellationToken>());
        pending.TrySetException(new IOException("history unavailable"));
        lifetime.Cancel();
        FluentActions.Invoking(() => reader.Capture(position, date, now)).Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task Initialization_captures_time_after_the_cash_read_and_gives_each_changed_capture_a_new_command_identity()
    {
        var databases = Substitute.For<IDbContextFactory>();
        var trades = Substitute.For<ITradeDbContext>();
        var market = Substitute.For<TomasAI.IFM.Application.Storage.MarketDataDb.IMarketDataDbContext>();
        var securities = Substitute.For<TomasAI.IFM.Application.Storage.SecuritiesDb.ISecuritiesDbContext>();
        var options = Substitute.For<TomasAI.IFM.Application.Storage.OptionPricerDb.IOptionPricerDbContext>();
        var financial = Substitute.For<TomasAI.IFM.Application.Storage.PortfolioFinancial.IFinancialQueryStore>();
        databases.TradeDb.Returns(trades); databases.MarketDataDb.Returns(market);
        databases.SecuritiesDb.Returns(securities); databases.OptionPricerDb.Returns(options);
        var (legs, _) = IronCondorOptionCalculatorTests.Evidence(atUtc: DateTimeOffset.UtcNow);
        var tradeId = new TradeEntityId(1, 2, 3, 4);
        var component = Guid.NewGuid();
        var now = DateTime.UtcNow.AddSeconds(-1);
        var date = DateOnly.FromDateTime(now);
        var trade = new EstablishedTradeDefinition { Id = tradeId, SourceComponentId = component,
            StrategyKind = TradeStrategyKind.IronCondor, Legs = legs, EstablishedAtUtc = now.AddDays(-1) };
        trades.GetEstablishedTradeAsync(tradeId, Arg.Any<CancellationToken>()).Returns(trade);
        trades.GetTradeOrderAsync(new(1, 2, 3), Arg.Any<CancellationToken>()).Returns(new TradeOrderDefinition
            { Revision = 1, RequiredCapital = 5000, Components = [new() { ComponentId = component }] });
        securities.GetFuturesOptionContractAsync(legs[0].ContractId, Arg.Any<CancellationToken>()).Returns(
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel { UnderlyingContractId = "ES-future" });
        trades.GetTradePlanForwardLossRatiosAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(
            Array.Empty<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanForwardLossRatioReadModel>());
        financial.ReadAsync(Arg.Any<TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialReadScope>(),
            Arg.Any<TomasAI.IFM.Domain.Portfolio.Shared.Financial.GetAccountBalancesRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialRead<TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialBalanceSnapshot>(
                TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialReadStatus.Found,
                new(1, "Active", [], 0, 100000, true), 7, DateTime.UtcNow)));
        var captured = new System.Collections.Concurrent.ConcurrentQueue<TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.InitializeIronCondorMonitoringCommand>();
        using var lifetime = new CancellationTokenSource();
        var reader = new IronCondorMonitoringInputReader(databases, Substitute.For<ILogger>(), financial,
            initializeMonitoring: (command, _) => { captured.Enqueue(command); return Task.CompletedTask; }, stoppingToken: lifetime.Token);
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(tradeId, TradeStrategyKind.IronCondor),
            Legs = legs.Select(leg => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                PutCall = leg.PutCall, Strike = leg.Strike, SignedQuantity = leg.SignedQuantity,
                OpeningPrice = leg.SignedQuantity > 0 ? 2 : 8 }).ToArray() };
        foreach (var evaluationAt in new[] { now, now.AddSeconds(6) })
        {
            var count = captured.Count;
            var elapsed = Stopwatch.StartNew();
            while (captured.Count == count && elapsed.Elapsed < TimeSpan.FromSeconds(3))
            { reader.Capture(position, date, evaluationAt); await Task.Delay(10); }
            captured.Count.Should().Be(count + 1);
        }
        var commands = captured.ToArray();
        commands.Select(command => command.CommandId).Distinct().Should().HaveCount(2);
        foreach (var command in commands)
        {
            command.MonitoringInitialization.InitializedAtUtc.Should().BeOnOrAfter(command.MonitoringInitialization.FundCashAsOfUtc);
            var state = new TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State.IronCondorPositionCommandState();
            state.ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.DomainEventCollection([
                new TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorPositionChangedEvent { EntityId = position.Id, PositionSnapshot = position }]));
            TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.InitializeIronCondorMonitoring.Execute(command, state).Success.Should().BeTrue();
        }
        lifetime.Cancel();
    }

    static TradeLegDefinition[] Legs(int direction) =>
    [
        new() { PutCall = 1, Strike = 6000, SignedQuantity = -direction },
        new() { PutCall = 1, Strike = 6100, SignedQuantity = direction },
        new() { PutCall = 2, Strike = 5600, SignedQuantity = -direction },
        new() { PutCall = 2, Strike = 5500, SignedQuantity = direction }
    ];
}
