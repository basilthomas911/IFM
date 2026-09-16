using TomasAI.IFM.Application.TradeBroker;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

/// <summary>Manual-entry economic behavior for the three first-release order shapes.</summary>
public sealed class ManualOrderEmulatorTests
{
    [Theory]
    [InlineData(BrokerOrderShape.FuturesOutright, 1)]
    [InlineData(BrokerOrderShape.VerticalSpread, 2)]
    [InlineData(BrokerOrderShape.IronCondor, 4)]
    public async Task Approved_manual_order_requires_complete_quote_then_changes_one_shared_account(BrokerOrderShape shape, int count)
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var clock = new ManualClock(now);
        var ledger = new EmulatorLedger(new EmulatorScenario("EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2)), clock);
        ITradeBroker broker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var legs = Enumerable.Range(0, count).Select(i => new BrokerOrderLeg(Guid.NewGuid(), $"CONTRACT-{i}", i % 2 == 0 ? 1 : -1, shape == BrokerOrderShape.FuturesOutright ? null : 100m + i, null, null, 1m)).ToArray();
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "portfolio/fund/order/component", Guid.NewGuid(), Guid.NewGuid(), shape, false, legs,
            200m, 200m, 200m, 0.05m, now.AddMinutes(5), "approval", "reference", 1_000m, 1_000m);
        var placed = await broker.PlaceAsync(request);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, placed.Outcome);
        Assert.False(ledger.TryMatch(request.BrokerOrderId, []));
        Assert.Equal(100_000m, (await broker.GetAccountSnapshotAsync()).CashBalance);
        Assert.Equal(99_000m, (await broker.GetAccountSnapshotAsync()).AvailableFunds);
        var quotes = legs.Select(l => new EmulatorQuote(l.ContractId, 99m, 100m, 10, 10, now, 1, 7)).ToArray();
        var matched = 0;
        foreach (var quote in quotes)
            matched += await broker.PublishMarketQuoteAsync(new BrokerMarketQuote(quote.ContractId,
                quote.Bid, quote.Ask, quote.BidSize, quote.AskSize, quote.MarketTimeUtc,
                quote.SourceEpoch, quote.SourceSequence));
        Assert.Equal(1, matched);
        Assert.False(ledger.TryMatch(request.BrokerOrderId, quotes));
        var facts = await broker.ReconcileOrderAsync(request.BrokerOrderId);
        Assert.Equal(count, facts.Count(x => x.Kind == BrokerObservationKind.Execution));
        Assert.Equal(count, facts.Count(x => x.Kind == BrokerObservationKind.Commission));
        var account = await broker.GetAccountSnapshotAsync();
        Assert.True(account.Complete);
        Assert.Equal(count, account.Positions.Length);
        Assert.True(account.CashBalance < 100_000m);
        Assert.Equal(placed, await broker.PlaceAsync(request));
    }

    [Fact]
    public async Task Different_instrument_sequences_form_a_coherent_snapshot_but_mixed_epoch_or_stale_quotes_do_not()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var clock = new ManualClock(now);
        var ledger = new EmulatorLedger(EmulatorScenario.Development("EMU"), clock);
        var broker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var legs = new[] { new BrokerOrderLeg(Guid.NewGuid(), "A", 1, 100, null, null, 1m), new BrokerOrderLeg(Guid.NewGuid(), "B", -1, 105, null, null, 1m) };
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "order", Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.VerticalSpread, false, legs,
            20m, 20m, 20m, 0.05m, now.AddMinutes(5), "approval", "reference", 1_000m, 1_000m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(request)).Outcome);
        Assert.False(ledger.TryMatch("order", [new EmulatorQuote("A", 10, 11, 1, 1, now, 1, 1)]));
        Assert.False(ledger.TryMatch("order", [new EmulatorQuote("A", 10, 11, 1, 1, now, 1, 1), new EmulatorQuote("B", 9, 10, 1, 1, now, 2, 2)]));
        clock.UtcNow = now.AddSeconds(3);
        Assert.False(ledger.TryMatch("order", [new EmulatorQuote("A", 10, 11, 1, 1, now, 1, 1), new EmulatorQuote("B", 9, 10, 1, 1, now, 1, 1)]));
        Assert.DoesNotContain(await broker.ReconcileOrderAsync("order"), x => x.Kind == BrokerObservationKind.Execution);

        clock.UtcNow = now;
        Assert.True(ledger.TryMatch("order", [new EmulatorQuote("A", 10, 11, 1, 1, now.AddMilliseconds(-10), 1, 11), new EmulatorQuote("B", 9, 10, 1, 1, now, 1, 29)]));
        var executions = (await broker.ReconcileOrderAsync("order"))
            .Where(x => x.Kind == BrokerObservationKind.Execution)
            .ToArray();
        Assert.Equal([11L, 29L], executions.Select(x => x.SourceSequence).ToArray());
    }

    [Fact]
    public async Task Different_payload_for_same_operation_is_classified_conflict()
    {
        var clock = new ManualClock(new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc));
        var ledger = new EmulatorLedger(EmulatorScenario.Development("EMU"), clock);
        var broker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "order", Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false,
            [new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m)], 5m, 5m, 5m, 0.05m, clock.UtcNow.AddMinutes(5), "approval", "ref", 1_000m, 1_000m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(request)).Outcome);
        var conflict = await broker.PlaceAsync(request with { SignedNetDebitLimit = 6m });
        Assert.Equal(BrokerDispatchOutcome.RejectedLocally, conflict.Outcome);
        Assert.Equal("EM.OPERATION.CONFLICT", conflict.Category);
    }

    [Fact]
    public async Task Insufficient_synthetic_cash_rejects_without_order_or_account_mutation()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var ledger = new EmulatorLedger(new EmulatorScenario("EMU", "USD", 100m, 0.65m, TimeSpan.FromSeconds(2)), new ManualClock(now));
        var broker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "order", Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false,
            [new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m)], 10m, 10m, 10m, 0.05m,
            now.AddMinutes(5), "approval", "reference", 200m, 200m);
        var receipt = await broker.PlaceAsync(request);
        Assert.Equal(BrokerDispatchOutcome.RejectedLocally, receipt.Outcome);
        Assert.Equal("EM.CASH.INSUFFICIENT", receipt.Category);
        Assert.Equal(100m, (await broker.GetAccountSnapshotAsync()).AvailableFunds);
        Assert.Empty(await broker.ReconcileOrderAsync("order"));
    }

    [Fact]
    public async Task Price_only_update_stays_in_envelope_and_cancel_releases_reservation()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var ledger = new EmulatorLedger(new EmulatorScenario("EMU", "USD", 100m, 0.65m, TimeSpan.FromSeconds(2)), new ManualClock(now));
        var broker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "order", Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false,
            [new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m)], 10m, 10m, 11m, 0.05m,
            now.AddMinutes(5), "approval", "reference", 20m, 20m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(request)).Outcome);
        Assert.Equal(80m, (await broker.GetAccountSnapshotAsync()).AvailableFunds);
        Assert.Equal("EM.LIMIT.INVALID", (await broker.ModifyLimitAsync(new("EMU", "order", Guid.NewGuid(), 12m, 1))).Category);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.ModifyLimitAsync(new("EMU", "order", Guid.NewGuid(), 10.5m, 1))).Outcome);
        Assert.Equal("EM.CANCEL.INVALID", (await broker.CancelAsync(new("EMU", "order", Guid.NewGuid(), 1))).Category);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.CancelAsync(new("EMU", "order", Guid.NewGuid(), 2))).Outcome);
        Assert.Equal(100m, (await broker.GetAccountSnapshotAsync()).AvailableFunds);
        Assert.DoesNotContain(await broker.ReconcileOrderAsync("order"), x => x.Kind == BrokerObservationKind.Execution);
    }

    [Fact]
    public async Task Fill_after_price_change_uses_the_latest_accepted_operation_identity()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var ledger = new EmulatorLedger(
            new EmulatorScenario("EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2)),
            new ManualClock(now));
        var broker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "updated-order",
            Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false,
            [new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m)],
            10m, 10m, 11m, 0.05m, now.AddMinutes(5), "approval", "reference", 20m, 20m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch,
            (await broker.PlaceAsync(request)).Outcome);
        var updateOperationId = Guid.NewGuid();
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch,
            (await broker.ModifyLimitAsync(new("EMU", request.BrokerOrderId,
                updateOperationId, 10.5m, 1))).Outcome);

        Assert.True(ledger.TryMatch(request.BrokerOrderId,
            [new EmulatorQuote("ES", 10m, 10.5m, 10, 10, now, 1, 2)]));

        var terminalFacts = (await broker.ReconcileOrderAsync(request.BrokerOrderId))
            .Where(fact => fact.Kind is BrokerObservationKind.Execution or
                BrokerObservationKind.Commission or BrokerObservationKind.OrderCompleted)
            .ToArray();
        Assert.NotEmpty(terminalFacts);
        Assert.All(terminalFacts, fact => Assert.Equal(updateOperationId, fact.OperationId));
    }

    [Fact]
    public async Task A_paper_mode_request_cannot_reach_the_emulator_ports()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var ledger = new EmulatorLedger(EmulatorScenario.Development("EMU"), new ManualClock(now));
        var broker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Paper, "order", Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false,
            [new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m)], 10m, 10m, 10m, 0.05m,
            now.AddMinutes(5), "approval", "reference", 20m, 20m);
        Assert.Equal("TB.ACCOUNT.MISMATCH", (await broker.PlaceAsync(request)).Category);
        Assert.Empty(ledger.Journal);
    }

    [Fact]
    public async Task Restart_recovers_the_same_order_account_and_observation_evidence()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var clock = new ManualClock(now);
        var store = new InMemoryEmulatorLedgerStore();
        var scenario = new EmulatorScenario("EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2));
        var first = new EmulatorLedger(scenario, clock, store);
        var firstBroker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(first), new EmulatedBrokerAccount(first));
        var leg = new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 50m);
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "1.2.3." + Guid.NewGuid().ToString("N") + "." + Guid.NewGuid().ToString("N"),
            Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false, [leg], 100m, 100m, 100m, 0.25m,
            now.AddMinutes(5), "approval", "reference", 10_000m, 10_000m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await firstBroker.PlaceAsync(request)).Outcome);
        Assert.True(first.TryMatch(request.BrokerOrderId, [new EmulatorQuote("ES", 99m, 100m, 10, 10, now, 1, 1)]));
        var expectedAccount = await firstBroker.GetAccountSnapshotAsync();
        var expectedFacts = await firstBroker.ReconcileOrderAsync(request.BrokerOrderId);
        var expectedJournal = first.Journal;

        var restarted = new EmulatorLedger(scenario, clock, store);
        var restartedBroker = new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(restarted), new EmulatedBrokerAccount(restarted));
        var recoveredAccount = await restartedBroker.GetAccountSnapshotAsync();
        Assert.Equal(expectedAccount.AccountAlias, recoveredAccount.AccountAlias);
        Assert.Equal(expectedAccount.Currency, recoveredAccount.Currency);
        Assert.Equal(expectedAccount.CashBalance, recoveredAccount.CashBalance);
        Assert.Equal(expectedAccount.AvailableFunds, recoveredAccount.AvailableFunds);
        Assert.Equal(expectedAccount.Complete, recoveredAccount.Complete);
        Assert.Equal(expectedAccount.NewRiskAllowed, recoveredAccount.NewRiskAllowed);
        Assert.Equal(expectedAccount.Generation, recoveredAccount.Generation);
        Assert.Equal(expectedAccount.AsOfUtc, recoveredAccount.AsOfUtc);
        Assert.Equal(expectedAccount.Positions, recoveredAccount.Positions);
        Assert.Equal(expectedFacts, await restartedBroker.ReconcileOrderAsync(request.BrokerOrderId));
        var recoveredJournal = restarted.Journal;
        Assert.Equal(expectedJournal.Length, recoveredJournal.Length);
        for (var index = 0; index < expectedJournal.Length; index++)
            Assert.Equal(expectedJournal[index], recoveredJournal[index]);
        Assert.Equal(await firstBroker.PlaceAsync(request), await restartedBroker.PlaceAsync(request));
        Assert.False(restarted.TryMatch(request.BrokerOrderId, [new EmulatorQuote("ES", 99m, 100m, 10, 10, now, 1, 1)]));
    }

    [Fact]
    public async Task Restart_returns_the_original_rejected_operation_receipt_without_revalidation()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var store = new InMemoryEmulatorLedgerStore();
        var scenario = new EmulatorScenario("EMU", "USD", 100m, 0.65m, TimeSpan.FromSeconds(2));
        var operationId = Guid.NewGuid();
        var request = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "rejected-order",
            operationId, Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false,
            [new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m)],
            10m, 10m, 10m, 0.05m, now.AddMinutes(5), "approval", "reference", 200m, 200m);
        var firstLedger = new EmulatorLedger(scenario, new ManualClock(now), store);
        var first = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(firstLedger), new EmulatedBrokerAccount(firstLedger));

        var rejected = await first.PlaceAsync(request);
        Assert.Equal(BrokerDispatchOutcome.RejectedLocally, rejected.Outcome);

        var restartedLedger = new EmulatorLedger(scenario, new ManualClock(now.AddMinutes(10)), store);
        var restarted = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(restartedLedger), new EmulatedBrokerAccount(restartedLedger));
        Assert.Equal(rejected, await restarted.PlaceAsync(request));
        Assert.Empty(restartedLedger.Journal);
    }

    [Fact]
    public async Task Opening_and_closing_fills_post_signed_cash_separate_commission_and_realized_result()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var clock = new ManualClock(now);
        var ledger = new EmulatorLedger(
            new EmulatorScenario("EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2)), clock);
        var broker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var openLeg = new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m);
        var opening = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "opening",
            Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, false, [openLeg],
            100m, 100m, 100m, 0.25m, now.AddMinutes(5), "approval", "reference", 100m, 100m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(opening)).Outcome);
        Assert.True(ledger.TryMatch("opening", [new EmulatorQuote("ES", 99m, 100m, 10, 10, now, 1, 1)]));
        var afterOpen = await broker.GetAccountSnapshotAsync();
        Assert.Equal(99_899.35m, afterOpen.CashBalance);
        Assert.Equal(1, Assert.Single(afterOpen.Positions).SignedQuantity);

        var closeLeg = new BrokerOrderLeg(Guid.NewGuid(), "ES", -1, null, null, null, 1m);
        var closing = new BrokerOrderRequest("EMU", BrokerEnvironment.Emulator, "closing",
            Guid.NewGuid(), Guid.NewGuid(), BrokerOrderShape.FuturesOutright, true, [closeLeg],
            -110m, -110m, -110m, 0.25m, now.AddMinutes(5), "approval", "reference", 1m, 1m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(closing)).Outcome);
        Assert.True(ledger.TryMatch("closing", [new EmulatorQuote("ES", 110m, 111m, 10, 10, now, 1, 2)]));
        var afterClose = await broker.GetAccountSnapshotAsync();
        Assert.Equal(100_008.70m, afterClose.CashBalance);
        Assert.Empty(afterClose.Positions);
        Assert.Equal(2, (await broker.ReconcileOrderAsync("opening"))
            .Count(fact => fact.Kind is BrokerObservationKind.Execution or BrokerObservationKind.Commission));
        Assert.Equal(2, (await broker.ReconcileOrderAsync("closing"))
            .Count(fact => fact.Kind is BrokerObservationKind.Execution or BrokerObservationKind.Commission));
    }

    [Fact]
    public async Task Balanced_partial_fill_survives_restart_and_completes_on_the_next_quote()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var clock = new ManualClock(now);
        var store = new InMemoryEmulatorLedgerStore();
        var scenario = new EmulatorScenario(
            "EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2),
            MaximumStrategyUnitsPerFill: 1);
        var legs = new[]
        {
            new BrokerOrderLeg(Guid.NewGuid(), "BUY", 2, 100m, null, null, 1m),
            new BrokerOrderLeg(Guid.NewGuid(), "SELL", -2, 105m, null, null, 1m)
        };
        var request = new BrokerOrderRequest(
            "EMU", BrokerEnvironment.Emulator, "partial-order", Guid.NewGuid(), Guid.NewGuid(),
            BrokerOrderShape.VerticalSpread, false, legs,
            2m, 2m, 2m, 0.05m, now.AddMinutes(5), "approval", "reference", 100m, 100m);
        var firstLedger = new EmulatorLedger(scenario, clock, store);
        var first = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(firstLedger), new EmulatedBrokerAccount(firstLedger));
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await first.PlaceAsync(request)).Outcome);

        var firstQuotes = new[]
        {
            new EmulatorQuote("BUY", 9m, 10m, 5, 5, now, 1, 10),
            new EmulatorQuote("SELL", 8m, 9m, 5, 5, now, 1, 11)
        };
        Assert.True(firstLedger.TryMatch(request.BrokerOrderId, firstQuotes));
        var firstFacts = await first.ReconcileOrderAsync(request.BrokerOrderId);
        Assert.Equal(2, firstFacts.Count(fact => fact.Kind == BrokerObservationKind.Execution));
        Assert.DoesNotContain(firstFacts, fact => fact.Kind == BrokerObservationKind.OrderCompleted);
        var firstPositionQuantities = (await first.GetAccountSnapshotAsync()).Positions
            .OrderBy(position => position.ContractId)
            .Select(position => position.SignedQuantity)
            .ToArray();
        Assert.Equal(new[] { 1, -1 }, firstPositionQuantities);

        var restartedLedger = new EmulatorLedger(scenario, clock, store);
        var restarted = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(restartedLedger), new EmulatedBrokerAccount(restartedLedger));
        var secondQuotes = new[]
        {
            new EmulatorQuote("BUY", 9m, 10m, 5, 5, now, 1, 12),
            new EmulatorQuote("SELL", 8m, 9m, 5, 5, now, 1, 13)
        };
        Assert.True(restartedLedger.TryMatch(request.BrokerOrderId, secondQuotes));
        var completedFacts = await restarted.ReconcileOrderAsync(request.BrokerOrderId);
        Assert.Equal(4, completedFacts.Count(fact => fact.Kind == BrokerObservationKind.Execution));
        Assert.Single(completedFacts, fact => fact.Kind == BrokerObservationKind.OrderCompleted);
        var completedPositionQuantities = (await restarted.GetAccountSnapshotAsync()).Positions
            .OrderBy(position => position.ContractId)
            .Select(position => position.SignedQuantity)
            .ToArray();
        Assert.Equal(new[] { 2, -2 }, completedPositionQuantities);
        Assert.False(restartedLedger.TryMatch(request.BrokerOrderId, secondQuotes));
    }

    [Fact]
    public async Task Unbalanced_quote_size_cannot_create_exposure_and_partial_cancel_keeps_only_confirmed_units()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var clock = new ManualClock(now);
        var ledger = new EmulatorLedger(
            new EmulatorScenario("EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2)), clock);
        var broker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var legs = new[]
        {
            new BrokerOrderLeg(Guid.NewGuid(), "BUY", 2, 100m, null, null, 1m),
            new BrokerOrderLeg(Guid.NewGuid(), "SELL", -2, 105m, null, null, 1m)
        };
        var request = new BrokerOrderRequest(
            "EMU", BrokerEnvironment.Emulator, "partial-cancel", Guid.NewGuid(), Guid.NewGuid(),
            BrokerOrderShape.VerticalSpread, false, legs,
            2m, 2m, 2m, 0.05m, now.AddMinutes(5), "approval", "reference", 100m, 100m);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(request)).Outcome);
        Assert.False(ledger.TryMatch(request.BrokerOrderId,
        [
            new EmulatorQuote("BUY", 9m, 10m, 2, 2, now, 1, 1),
            new EmulatorQuote("SELL", 8m, 9m, 0, 0, now, 1, 2)
        ]));
        Assert.Empty((await broker.GetAccountSnapshotAsync()).Positions);

        Assert.True(ledger.TryMatch(request.BrokerOrderId,
        [
            new EmulatorQuote("BUY", 9m, 10m, 1, 1, now, 1, 3),
            new EmulatorQuote("SELL", 8m, 9m, 1, 1, now, 1, 4)
        ]));
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch,
            (await broker.CancelAsync(new("EMU", request.BrokerOrderId, Guid.NewGuid(), 1))).Outcome);
        var remainingPositionQuantities = (await broker.GetAccountSnapshotAsync()).Positions
            .OrderBy(position => position.ContractId)
            .Select(position => position.SignedQuantity)
            .ToArray();
        Assert.Equal(new[] { 1, -1 }, remainingPositionQuantities);
        var facts = await broker.ReconcileOrderAsync(request.BrokerOrderId);
        Assert.DoesNotContain(facts, fact => fact.Kind == BrokerObservationKind.OrderCompleted);
        Assert.Single(facts, fact => fact.Kind == BrokerObservationKind.Cancelled);
    }

    [Fact]
    public async Task Ambiguous_dispatch_without_ack_can_still_reconcile_an_authoritative_fill()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var ledger = new EmulatorLedger(new EmulatorScenario(
            "EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2),
            Faults: new EmulatorFaultProfile(
                PlaceDispatchOutcomeUnknown: true,
                SuppressAcknowledgement: true)), new ManualClock(now));
        var broker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var request = FuturesRequest("ambiguous", now);

        var receipt = await broker.PlaceAsync(request);

        Assert.Equal(BrokerDispatchOutcome.OutcomeUnknown, receipt.Outcome);
        Assert.Empty(await broker.ReconcileOrderAsync(request.BrokerOrderId));
        Assert.True(ledger.TryMatch(request.BrokerOrderId,
            [new EmulatorQuote("ES", 9m, 10m, 1, 1, now, 1, 1)]));
        var facts = await broker.ReconcileOrderAsync(request.BrokerOrderId);
        Assert.DoesNotContain(facts, fact => fact.Kind == BrokerObservationKind.Acknowledged);
        Assert.Contains(facts, fact => fact.Kind == BrokerObservationKind.Execution);
        Assert.Contains(facts, fact => fact.Kind == BrokerObservationKind.OrderCompleted);
    }

    [Fact]
    public async Task Declared_callback_faults_publish_fee_first_and_exact_duplicate_execution_callbacks()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var ledger = new EmulatorLedger(new EmulatorScenario(
            "EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2),
            Faults: new EmulatorFaultProfile(
                PublishCommissionBeforeExecution: true,
                DuplicateExecutionCallbacks: true)), new ManualClock(now));
        var broker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var request = FuturesRequest("fault-order", now);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await broker.PlaceAsync(request)).Outcome);
        Assert.True(ledger.TryMatch(request.BrokerOrderId,
            [new EmulatorQuote("ES", 9m, 10m, 1, 1, now, 1, 1)]));

        var reconciled = await broker.ReconcileOrderAsync(request.BrokerOrderId);
        Assert.True(Array.FindIndex(reconciled, fact => fact.Kind == BrokerObservationKind.Commission) <
            Array.FindIndex(reconciled, fact => fact.Kind == BrokerObservationKind.Execution));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var reader = broker.ObserveOrdersAsync(timeout.Token).GetAsyncEnumerator();
        var callbacks = new List<BrokerObservation>();
        while (callbacks.Count < 6 && await reader.MoveNextAsync())
            callbacks.Add(reader.Current);
        var executions = callbacks.Where(fact => fact.Kind == BrokerObservationKind.Execution).ToArray();
        var commissions = callbacks.Where(fact => fact.Kind == BrokerObservationKind.Commission).ToArray();
        Assert.Equal(2, executions.Length);
        Assert.Equal(executions[0].ObservationId, executions[1].ObservationId);
        Assert.Equal(2, commissions.Length);
        Assert.Equal(commissions[0].ObservationId, commissions[1].ObservationId);
    }

    [Fact]
    public async Task Cancel_fill_race_preserves_a_declared_late_fill_while_disconnect_prevents_matching()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var lateFillLedger = new EmulatorLedger(new EmulatorScenario(
            "EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2),
            Faults: new EmulatorFaultProfile(AllowFillsAfterCancel: true)), new ManualClock(now));
        var lateFillBroker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(lateFillLedger), new EmulatedBrokerAccount(lateFillLedger));
        var request = FuturesRequest("cancel-race", now);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, (await lateFillBroker.PlaceAsync(request)).Outcome);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch,
            (await lateFillBroker.CancelAsync(new("EMU", request.BrokerOrderId, Guid.NewGuid(), 1))).Outcome);
        Assert.True(lateFillLedger.TryMatch(request.BrokerOrderId,
            [new EmulatorQuote("ES", 9m, 10m, 1, 1, now, 1, 1)]));
        var facts = await lateFillBroker.ReconcileOrderAsync(request.BrokerOrderId);
        Assert.True(Array.FindIndex(facts, fact => fact.Kind == BrokerObservationKind.Cancelled) <
            Array.FindIndex(facts, fact => fact.Kind == BrokerObservationKind.Execution));

        var disconnectedLedger = new EmulatorLedger(new EmulatorScenario(
            "EMU", "USD", 100_000m, 0.65m, TimeSpan.FromSeconds(2),
            Faults: new EmulatorFaultProfile(DisconnectMarketMatching: true)), new ManualClock(now));
        var disconnectedBroker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(disconnectedLedger), new EmulatedBrokerAccount(disconnectedLedger));
        var disconnected = FuturesRequest("disconnected", now);
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch,
            (await disconnectedBroker.PlaceAsync(disconnected)).Outcome);
        Assert.False(disconnectedLedger.TryMatch(disconnected.BrokerOrderId,
            [new EmulatorQuote("ES", 9m, 10m, 1, 1, now, 1, 1)]));
        Assert.DoesNotContain(await disconnectedBroker.ReconcileOrderAsync(disconnected.BrokerOrderId),
            fact => fact.Kind == BrokerObservationKind.Execution);
    }

    private static BrokerOrderRequest FuturesRequest(string brokerOrderId, DateTime now) => new(
        "EMU", BrokerEnvironment.Emulator, brokerOrderId, Guid.NewGuid(), Guid.NewGuid(),
        BrokerOrderShape.FuturesOutright, false,
        [new BrokerOrderLeg(Guid.NewGuid(), "ES", 1, null, null, null, 1m)],
        10m, 10m, 10m, 0.05m, now.AddMinutes(5),
        "approval", "reference", 100m, 100m);

    private sealed class ManualClock(DateTime utcNow) : IEmulatorClock { public DateTime UtcNow { get; set; } = utcNow; }
}
