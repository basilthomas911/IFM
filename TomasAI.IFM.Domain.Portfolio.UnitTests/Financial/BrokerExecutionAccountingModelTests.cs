using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

public sealed class BrokerExecutionAccountingModelTests
{
    [Fact]
    public void Opening_fill_creates_confirmed_settlement_and_separate_commission()
    {
        var fixture = Fixture(TradeOrderPositionType.Opening, 1, 100m, 2.50m);
        var batch = BrokerExecutionAccountingModel.Create(fixture.Input);
        batch.Items.Should().HaveCount(2);
        batch.Items[0].TransactionKind.Should().Be(LedgerTransactionKind.TradeSettlement);
        batch.Items[0].Amount.Should().Be(5_000m);
        batch.Items[0].MovementEvidence.Status.Should().Be(MovementStatus.Confirmed);
        batch.Items[1].TransactionKind.Should().Be(LedgerTransactionKind.Commission);
        batch.Items[1].Amount.Should().Be(2.50m);
        batch.Items.Select(x => x.Source.FillId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Closing_fill_posts_signed_credit_and_realized_profit_from_exact_opening_basis()
    {
        var fixture = Fixture(TradeOrderPositionType.Closing, -1, 120m, 2.50m);
        var input = fixture.Input with { OpeningSignedSettlementByLeg = new Dictionary<Guid, decimal> { [fixture.LegId] = 5_000m } };
        var batch = BrokerExecutionAccountingModel.Create(input);
        batch.Items.Single(x => x.TransactionKind == LedgerTransactionKind.TradeSettlement).Amount.Should().Be(-6_000m);
        batch.Items.Single(x => x.TransactionKind == LedgerTransactionKind.RealizedPnl).Amount.Should().Be(1_000m);
        batch.Items.Single(x => x.TransactionKind == LedgerTransactionKind.Commission).Amount.Should().Be(2.50m);
    }

    [Theory]
    [InlineData(-1, 120, 122, 2100)]
    [InlineData(-1, 80, 82, -1900)]
    [InlineData(1, 80, 82, 1900)]
    [InlineData(1, 120, 122, -2100)]
    public void Split_closing_fills_consume_opening_basis_only_once_per_leg(
        int direction, decimal firstPrice, decimal secondPrice, decimal expectedProfit)
    {
        // Two contracts opened at 100, closed in separate one-contract fills.
        // A single per-leg opening basis must be consumed regardless of fragmentation.
        var fixture = Fixture(TradeOrderPositionType.Closing, direction * 2, firstPrice, 2.50m);
        var first = fixture.Input.Fills[0] with { SignedQuantity = direction };
        var second = first with { ExecutionFillId = Guid.NewGuid(), ExternalExecutionId = "EM-EXEC-2", Price = secondPrice };
        var input = fixture.Input with
        {
            Fills = [first, second],
            OpeningSignedSettlementByLeg = new Dictionary<Guid, decimal> { [fixture.LegId] = -direction * 10_000m }
        };
        var batch = BrokerExecutionAccountingModel.Create(input);
        batch.Items.Where(x => x.TransactionKind == LedgerTransactionKind.TradeSettlement).Sum(x => x.Amount)
            .Should().Be(direction * (firstPrice + secondPrice) * 50m);
        batch.Items.Where(x => x.TransactionKind == LedgerTransactionKind.Commission).Sum(x => x.Amount).Should().Be(5m);
        batch.Items.Single(x => x.TransactionKind == LedgerTransactionKind.RealizedPnl).Amount.Should().Be(expectedProfit);
        var reversed = BrokerExecutionAccountingModel.Create(input with { Fills = [second, first] });
        reversed.ManifestHash.Should().Be(batch.ManifestHash);
    }

    [Fact]
    public void Break_even_close_still_emits_realization_to_clear_existing_MTM()
    {
        var fixture=Fixture(TradeOrderPositionType.Closing,-1,100m,0m);
        var input=fixture.Input with { OpeningSignedSettlementByLeg=new Dictionary<Guid,decimal> { [fixture.LegId]=5000m } };
        BrokerExecutionAccountingModel.Create(input).Items.Single(x=>x.TransactionKind==LedgerTransactionKind.RealizedPnl)
            .Amount.Should().Be(0m);
    }

    [Fact]
    public void Missing_basis_or_unconfirmed_settlement_rule_fails_closed()
    {
        var fixture = Fixture(TradeOrderPositionType.Closing, -1, 120m, 0m);
        FluentActions.Invoking(() => BrokerExecutionAccountingModel.Create(fixture.Input)).Should().Throw<ArgumentException>();
        var broken = fixture.Input with
        {
            Configuration = fixture.Input.Configuration with
            {
                Rules = fixture.Input.Configuration.Rules.Select(x => x.Kind == LedgerTransactionKind.TradeSettlement
                    ? x with { RequiresConfirmedMovement = false } : x).ToArray()
            },
            OpeningSignedSettlementByLeg = new Dictionary<Guid, decimal> { [fixture.LegId] = 5_000m }
        };
        FluentActions.Invoking(() => BrokerExecutionAccountingModel.Create(broken)).Should().Throw<InvalidOperationException>();
    }

    private static (BrokerExecutionAccountingInput Input, Guid LegId) Fixture(TradeOrderPositionType positionType,
        int signedQuantity, decimal price, decimal commission)
    {
        var now = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var legId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var rules = new[]
        {
            Rule(LedgerTransactionKind.TradeSettlement, 1, 2, true),
            Rule(LedgerTransactionKind.Commission, 3, 2, true),
            Rule(LedgerTransactionKind.RealizedPnl, 2, 4, true)
        };
        var configuration = new FinancialPostingConfiguration(11, 2, new FinancialAuthorityReference(), rules, true, DateOnly.FromDateTime(now));
        var order = new TradeOrderDefinition
        {
            Id = new(1, 2, 3), PositionType = positionType, ValueDate = DateOnly.FromDateTime(now),
            Components = [new TradeOrderComponentDefinition
            {
                ComponentId = componentId, ReservedTradeId = 4, StrategyKind = TradeStrategyKind.FuturesOutright,
                Legs = [new TradeLegDefinition { TradeLegId = legId, ContractId = "ES", SignedQuantity = signedQuantity, CashMultiplier = 50m }]
            }]
        };
        var fill = new ExecutionFillEvidence
        {
            ExecutionFillId = Guid.NewGuid(), ExecutionAttemptId = Guid.NewGuid(), ComponentId = componentId,
            TradeLegId = legId, ContractId = "ES", SignedQuantity = signedQuantity, Price = price,
            Commission = commission, FilledAtUtc = now, ExternalExecutionId = "EM-EXEC-1"
        };
        return (new(configuration, order, [fill], "EM-MOVEMENT-1", now.AddMilliseconds(1)), legId);
    }

    private static LedgerPostingRule Rule(LedgerTransactionKind kind, int debit, int credit, bool movement) =>
        new(Guid.NewGuid(), 1, $"{kind}-hash", kind, new(debit, 1), new(credit, 1), movement,
            kind == LedgerTransactionKind.RealizedPnl ? new(5, 1) : null,
            kind == LedgerTransactionKind.RealizedPnl ? new(6, 1) : null);
}
