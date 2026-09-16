using TomasAI.IFM.Application.TradeBroker;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

/// <summary>Deterministic qualification soak for repeated partial fills and checkpoint restarts.</summary>
public sealed class EmulatorDeterminismSoakTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Repeated_three_shape_partial_fill_and_restart_runs_finish_with_the_same_ledger_hash()
    {
        var first = await RunAsync();
        var second = await RunAsync();

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Cash, second.Cash);
        Assert.Equal(first.Generation, second.Generation);
        Assert.Equal(96 * 2, first.Generation - 1);
        output.WriteLine("Orders=96; PartialFillCycles=192; Restarts=3; LedgerHash={0}; Cash={1}; Generation={2}",
            first.Hash, first.Cash, first.Generation);
    }

    private static async Task<(string Hash, decimal Cash, long Generation)> RunAsync()
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var clock = new ManualClock(now);
        var store = new InMemoryEmulatorLedgerStore();
        var scenario = new EmulatorScenario(
            "IFM-EMULATOR-PAPER", "USD", 10_000_000m, 0.65m,
            TimeSpan.FromSeconds(2), MaximumStrategyUnitsPerFill: 1);
        EmulatorLedger ledger = new(scenario, clock, store);

        for (var orderIndex = 0; orderIndex < 96; orderIndex++)
        {
            if (orderIndex > 0 && orderIndex % 24 == 0)
                ledger = new EmulatorLedger(scenario, clock, store);
            var broker = new InteractiveBrokersEmulatorTradeBroker(
                new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
            var shape = (orderIndex % 3) switch
            {
                0 => BrokerOrderShape.FuturesOutright,
                1 => BrokerOrderShape.VerticalSpread,
                _ => BrokerOrderShape.IronCondor
            };
            var legCount = shape switch
            {
                BrokerOrderShape.FuturesOutright => 1,
                BrokerOrderShape.VerticalSpread => 2,
                _ => 4
            };
            var legs = Enumerable.Range(0, legCount)
                .Select(legIndex => new BrokerOrderLeg(
                    GuidFrom(orderIndex * 10 + legIndex + 10_000),
                    $"SOAK-{orderIndex:D3}-{legIndex}",
                    legIndex % 2 == 0 ? 2 : -2,
                    shape == BrokerOrderShape.FuturesOutright ? null : 100m + legIndex,
                    null, null, 1m))
                .ToArray();
            var limit = shape switch
            {
                BrokerOrderShape.FuturesOutright => 10m,
                BrokerOrderShape.VerticalSpread => 2m,
                _ => 4m
            };
            var request = new BrokerOrderRequest(
                scenario.AccountAlias, BrokerEnvironment.Emulator, $"SOAK-{orderIndex:D3}",
                GuidFrom(orderIndex * 10 + 1), GuidFrom(orderIndex * 10 + 2), shape, false,
                legs, limit, limit, limit, 0.05m, now.AddHours(1),
                "approval", "qualification-soak", 1_000m, 1_000m);
            Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch,
                (await broker.PlaceAsync(request)).Outcome);

            for (var cycle = 0; cycle < 2; cycle++)
            {
                var quotes = legs.Select((leg, legIndex) => new EmulatorQuote(
                    leg.ContractId, 8m, 10m, 10, 10, now, 1,
                    orderIndex * 100L + cycle * 10L + legIndex + 1)).ToArray();
                Assert.True(ledger.TryMatch(request.BrokerOrderId, quotes));
            }
            Assert.Single(await broker.ReconcileOrderAsync(request.BrokerOrderId),
                fact => fact.Kind == BrokerObservationKind.OrderCompleted);
        }

        var finalLedger = new EmulatorLedger(scenario, clock, store);
        var snapshot = finalLedger.Snapshot();
        return (finalLedger.LedgerHash, snapshot.CashBalance, snapshot.Generation);
    }

    private static Guid GuidFrom(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

    private sealed class ManualClock(DateTime utcNow) : IEmulatorClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
