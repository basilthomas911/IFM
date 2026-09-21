using TomasAI.IFM.Application.TradeBroker;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.BrokerAccount;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class BrokerCapabilityTests
{
    [Theory]
    [InlineData(BrokerOrderType.Market, BrokerAlgorithm.None)]
    [InlineData(BrokerOrderType.Limit, BrokerAlgorithm.None)]
    [InlineData(BrokerOrderType.Market, BrokerAlgorithm.Adaptive)]
    [InlineData(BrokerOrderType.Limit, BrokerAlgorithm.Adaptive)]
    public async Task Emulator_advertises_and_accepts_qualified_order_type_algorithm_pairs(
        BrokerOrderType orderType, BrokerAlgorithm algorithm)
    {
        var broker = Broker();
        Assert.Equal(BrokerEnvironment.Emulator, broker.Capabilities.Environment);
        Assert.Contains(orderType, broker.Capabilities.OrderTypes);
        Assert.Contains(algorithm, broker.Capabilities.Algorithms);
        var receipt = await broker.PlaceAsync(Request(orderType, algorithm));
        Assert.Equal(BrokerDispatchOutcome.AcceptedForDispatch, receipt.Outcome);
    }

    [Fact]
    public async Task Unsupported_or_wrong_account_combination_is_rejected_before_framework_dispatch()
    {
        var now = new DateTime(2026, 9, 20, 14, 0, 0, DateTimeKind.Utc);
        var ledger = new EmulatorLedger(EmulatorScenario.Development("EMU"), new Clock(now));
        ITradeBroker broker = new InteractiveBrokersEmulatorTradeBroker(
            new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
        var unsupported = Request(BrokerOrderType.Unknown, BrokerAlgorithm.Adaptive, now);
        var receipt = await broker.PlaceAsync(unsupported);
        Assert.Equal(BrokerDispatchOutcome.RejectedLocally, receipt.Outcome);
        Assert.Equal("TB.CAPABILITY.UNSUPPORTED", receipt.Category);
        Assert.Empty(ledger.Journal);
        Assert.NotNull(broker.Capabilities.Validate(unsupported with { AccountAlias = "OTHER", OrderType = BrokerOrderType.Limit }));
    }

    [Fact]
    public void Ibkr_modes_are_derived_from_the_loaded_account_environment()
    {
        Assert.Equal(BrokerEnvironment.Paper, BrokerCapabilities.InteractiveBrokers("PAPER", BrokerEnvironment.Paper).Environment);
        Assert.Equal(BrokerEnvironment.Live, BrokerCapabilities.InteractiveBrokers("LIVE", BrokerEnvironment.Live).Environment);
        Assert.Throws<ArgumentOutOfRangeException>(() => BrokerCapabilities.InteractiveBrokers("EMU", BrokerEnvironment.Emulator));
    }

    private static ITradeBroker Broker()
    {
        var ledger = new EmulatorLedger(EmulatorScenario.Development("EMU"),
            new Clock(new DateTime(2026, 9, 20, 14, 0, 0, DateTimeKind.Utc)));
        return new InteractiveBrokersEmulatorTradeBroker(new EmulatedOrderExecutionBroker(ledger), new EmulatedBrokerAccount(ledger));
    }

    private static BrokerOrderRequest Request(BrokerOrderType orderType, BrokerAlgorithm algorithm, DateTime? at = null)
    {
        var now = at ?? new DateTime(2026, 9, 20, 14, 0, 0, DateTimeKind.Utc);
        return new("EMU", BrokerEnvironment.Emulator, Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.NewGuid(),
            BrokerOrderShape.FuturesOutright, false,
            [new(Guid.NewGuid(), "ESZ6", 1, null, null, null, 50m)], 6000m, 5900m, 6100m, .25m,
            now.AddMinutes(5), "approval", "reference", 10_000m, 10_000m, orderType, algorithm);
    }

    private sealed class Clock(DateTime now) : IEmulatorClock { public DateTime UtcNow { get; } = now; }
}
