using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using DomainBrokerEnvironment = TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment;

namespace TomasAI.IFM.Domain.Trade.Benchmarks;

/// <summary>Allocation and throughput probes for the emulator paths used by broker actors.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class TradeBrokerEmulatorBenchmarks
{
    private readonly DateTime _now = new(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
    private TradeOrderDefinition _order = new();
    private Guid _attemptId;
    private Guid _componentId;
    private Guid _operationId;
    private EmulatorLedger _ledger = null!;
    private EmulatorQuote _nonCrossingQuote;
    private MicroExecutionPolicy _policy = null!;
    private MicroExecutionInput _input = null!;

    /// <summary>Creates stable benchmark inputs and one working, deliberately non-crossing order.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _attemptId = Guid.NewGuid();
        _componentId = Guid.NewGuid();
        _operationId = Guid.NewGuid();
        _order = new TradeOrderDefinition
        {
            Id = new(1, 2, 3),
            Revision = 1,
            Status = TradeOrderStatus.Approved,
            PositionType = TradeOrderPositionType.Opening,
            PortfolioApprovalId = Guid.NewGuid(),
            BrokerAccountAlias = "IFM-EMULATOR-PAPER",
            BrokerEnvironment = DomainBrokerEnvironment.Emulator,
            DefinitionHash = "definition",
            MicroExecutionProfileHash = "profile",
            RequiredCapital = 10_000m,
            MaximumLoss = 10_000m,
            ValidUntilUtc = _now.AddHours(1),
            Components =
            [
                new TradeOrderComponentDefinition
                {
                    ComponentId = _componentId,
                    ReservedTradeId = 4,
                    StrategyKind = TradeStrategyKind.FuturesOutright,
                    SignedNetDebitLimit = 100m,
                    MinimumSignedNetDebitLimit = 90m,
                    MaximumSignedNetDebitLimit = 110m,
                    TickIncrement = 0.25m,
                    Legs =
                    [
                        new TradeLegDefinition
                        {
                            TradeLegId = Guid.NewGuid(),
                            ContractId = "ESZ6",
                            ContractKey = "ESZ6",
                            AssetFamily = TradeAssetFamily.Futures,
                            SignedQuantity = 1,
                            CashMultiplier = 50m
                        }
                    ]
                }
            ]
        };
        _ledger = new EmulatorLedger(
            new EmulatorScenario("IFM-EMULATOR-PAPER", "USD", 1_000_000m, 0.65m,
                TimeSpan.FromSeconds(2)), new FixedClock(_now));
        if (!BrokerOrderRequestMapper.TryCreate(_order, _attemptId, _componentId,
                _operationId, out var request, out var reason) || request is null)
            throw new InvalidOperationException(reason);
        _ledger.Place(TomasAI.IFM.Application.TradeBroker.Mapping.TradeBrokerMapper.ToFramework(request));
        _nonCrossingQuote = new("ESZ6", 6000m, 6000.25m, 10, 10, _now, 1, 1);
        _policy = new(new MicroExecutionConstraintEvaluator(), new MicroExecutionPriceCalculator());
        _input = new MicroExecutionInput(
            new("ManualExactLimit", 1, "profile", TimeSpan.FromMilliseconds(250),
                TimeSpan.FromSeconds(2), 4, false), true, true, true, false, false,
            100m, 90m, 110m, 0.25m, 99.75m, 100m, _now, _now,
            _now.AddSeconds(10), null, 0);
    }

    /// <summary>Measures TradeOrder-to-broker request validation and mapping.</summary>
    [Benchmark]
    public bool MapOrder() => BrokerOrderRequestMapper.TryCreate(
        _order, _attemptId, _componentId, _operationId, out _, out _);

    /// <summary>Measures the hot exact-contract quote filter with no resulting fill.</summary>
    [Benchmark]
    public int FilterQuote() => _ledger.PublishQuote(_nonCrossingQuote);

    /// <summary>Measures an immutable account snapshot read.</summary>
    [Benchmark]
    public decimal ReadAccountSnapshot() => _ledger.Snapshot().AvailableFunds;

    /// <summary>Measures one pure micro-execution decision.</summary>
    [Benchmark]
    public MicroExecutionDecision DecideMicroExecution() => _policy.Decide(_input);

    private sealed class FixedClock(DateTime utcNow) : IEmulatorClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
