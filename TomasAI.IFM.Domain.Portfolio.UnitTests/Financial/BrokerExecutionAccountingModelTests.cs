using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

public sealed class BrokerExecutionAccountingModelTests
{
    [Fact]
    public void Later_commission_does_not_change_the_automatic_fill_settlement_identity()
    {
        var fixture = Fixture(TradeOrderPositionType.Opening, 1, 100m, 0m);
        var input = fixture.Input with { Order = fixture.Input.Order with { StrategyWorkflowId = Guid.NewGuid() } };
        var initial = BrokerExecutionAccountingModel.Create(input);
        var updated = BrokerExecutionAccountingModel.Create(input with { Fills = [input.Fills[0] with { Commission = 2.50m }] });
        var settlement = initial.Items.Single(item => item.TransactionKind == LedgerTransactionKind.TradeSettlement);
        var repeated = updated.Items.Single(item => item.TransactionKind == LedgerTransactionKind.TradeSettlement);
        repeated.Source.Should().Be(settlement.Source);
        updated.Items.Single(item => item.TransactionKind == LedgerTransactionKind.Commission).Source.SourceContentHash
            .Should().NotBe(settlement.Source.SourceContentHash);
    }

    [Fact]
    public void Automatic_futures_opening_retains_the_verified_manual_emulator_cash_economics()
    {
        var fixture = Fixture(TradeOrderPositionType.Opening, 1, 6000m, 2.50m);
        var component = fixture.Input.Order.Components.Single();
        var input = fixture.Input with { Order = fixture.Input.Order with
        {
            StrategyWorkflowId = Guid.NewGuid(), Components = [component with
            { Legs = component.Legs.Select(leg => leg with { AssetFamily = TradeAssetFamily.Futures }).ToArray() }]
        } };
        var batch = BrokerExecutionAccountingModel.Create(input);
        batch.Items.Should().HaveCount(2);
        batch.Items.Single(item => item.TransactionKind == LedgerTransactionKind.TradeSettlement).Amount.Should().Be(300000m);
        batch.Items.Single(item => item.TransactionKind == LedgerTransactionKind.Commission).Amount.Should().Be(2.50m);
    }

    [Fact]
    public void Historical_multiplier_delta_links_original_journals_without_reposting_fills_or_commissions()
    {
        var configuration = Fixture(TradeOrderPositionType.Opening, -1, 12.45m, .65m).Input.Configuration;
        var component = Guid.NewGuid(); var attempt = Guid.NewGuid(); var amendment = Guid.NewGuid();
        var at = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);
        var quantities = new[] { -1, 1, -1, 1 }; var prices = new[] { 12.45m, 1m, 1m, 1m };
        var legs = Enumerable.Range(0, 4).Select(index => new TradeLegDefinition {
            TradeLegId = Guid.NewGuid(), ContractId = $"ES-LEG-{index}", SignedQuantity = quantities[index], CashMultiplier = 1
        }).ToArray();
        var fills = legs.Select((leg, index) => new ExecutionFillEvidence {
            ExecutionFillId = Guid.NewGuid(), ExecutionAttemptId = attempt, ComponentId = component,
            TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
            Price = prices[index], Commission = .65m, FilledAtUtc = at.AddDays(-20), ExternalExecutionId = $"original-{index}"
        }).ToArray();
        var original = new EstablishedTradeDefinition { Id = new(1,2,3,4), Legs = legs, OriginalFills = fills,
            SourceComponentId = component, ExecutionAttemptId = attempt, EvidenceRevision = 1 };
        var corrected = legs.Select(leg => leg with { CashMultiplier = 50 }).ToArray();
        var posted = fills.Select((fill, index) => new { fill.ExecutionFillId,
            Posting = new PostedOpeningSettlement(301 + index, fill.Price * fill.SignedQuantity) })
            .ToDictionary(item => item.ExecutionFillId, item => item.Posting);
        var batch = OpeningMultiplierReconciliation.Create(original, corrected, configuration, posted, amendment, at);
        batch.Items.Should().HaveCount(4);
        batch.Items.Sum(item => item.Amount).Should().Be(-561.05m);
        batch.Items.Should().OnlyContain(item => item.TransactionKind == LedgerTransactionKind.TradeSettlement);
        batch.Items.Select(item => item.RelatedJournalId).Should().Equal(301L,302L,303L,304L);
        batch.Items.Select(item => item.Source.FillId).Should().OnlyHaveUniqueItems();
        batch.Items.Select(item => item.Source.SourceEventId).Should().OnlyHaveUniqueItems();
        OpeningMultiplierReconciliation.Create(original, corrected, configuration, posted, amendment, at).ManifestHash
            .Should().Be(batch.ManifestHash);
        original.Legs.Should().OnlyContain(leg => leg.CashMultiplier == 1);
        original.OriginalFills.Should().Equal(fills);
        var wrong = posted.ToDictionary(item => item.Key, item => item.Value);
        wrong[fills[0].ExecutionFillId] = new(301, -99);
        ((Action)(() => OpeningMultiplierReconciliation.Create(original, corrected, configuration, wrong, amendment, at)))
            .Should().Throw<ArgumentException>();
    }
    [Fact]
    public void Accepted_fill_hashes_are_independent_of_process_culture()
    {
        var fixture = Fixture(TradeOrderPositionType.Opening, 1, 100.25m, 2.50m);
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var first = BrokerExecutionAccountingModel.Create(fixture.Input);
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var second = BrokerExecutionAccountingModel.Create(fixture.Input);
            second.ManifestHash.Should().Be(first.ManifestHash);
            second.Items.Select(x => x.Source.SourceContentHash)
                .Should().Equal(first.Items.Select(x => x.Source.SourceContentHash));
            second.Items.Select(x => x.MovementEvidence.ContentHash)
                .Should().Equal(first.Items.Select(x => x.MovementEvidence.ContentHash));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

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
        var fixture = Fixture(TradeOrderPositionType.Closing, -1, 100m, 0m);
        var input = fixture.Input with { OpeningSignedSettlementByLeg = new Dictionary<Guid, decimal> { [fixture.LegId] = 5000m } };
        BrokerExecutionAccountingModel.Create(input).Items.Single(x => x.TransactionKind == LedgerTransactionKind.RealizedPnl)
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
            Id = new(1, 2, 3),
            PositionType = positionType,
            ValueDate = DateOnly.FromDateTime(now),
            Components = [new TradeOrderComponentDefinition
            {
                ComponentId = componentId, ReservedTradeId = 4, StrategyKind = TradeStrategyKind.FuturesOutright,
                Legs = [new TradeLegDefinition { TradeLegId = legId, ContractId = "ES", SignedQuantity = signedQuantity, CashMultiplier = 50m }]
            }]
        };
        var fill = new ExecutionFillEvidence
        {
            ExecutionFillId = Guid.NewGuid(),
            ExecutionAttemptId = Guid.NewGuid(),
            ComponentId = componentId,
            TradeLegId = legId,
            ContractId = "ES",
            SignedQuantity = signedQuantity,
            Price = price,
            Commission = commission,
            FilledAtUtc = now,
            ExternalExecutionId = "EM-EXEC-1"
        };
        return (new(configuration, order, [fill], "EM-MOVEMENT-1", now.AddMilliseconds(1)), legId);
    }

    private static LedgerPostingRule Rule(LedgerTransactionKind kind, int debit, int credit, bool movement) =>
        new(Guid.NewGuid(), 1, $"{kind}-hash", kind, new(debit, 1), new(credit, 1), movement,
            kind == LedgerTransactionKind.RealizedPnl ? new(5, 1) : null,
            kind == LedgerTransactionKind.RealizedPnl ? new(6, 1) : null);
}
