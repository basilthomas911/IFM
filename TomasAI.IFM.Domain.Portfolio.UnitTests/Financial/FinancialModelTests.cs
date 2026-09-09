using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

[Trait("Category", "PortfolioFinancial")]
public sealed class FinancialModelTests
{
    static readonly LedgerAccountBinding Cash = new(101, 1), Equity = new(102, 1);

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(100, 99.99, false)]
    public void Journal_balancing_uses_exact_cents(decimal debit, decimal credit, bool valid)
    {
        PlannedLedgerLine[] lines = [new(1,Cash,1,debit,0,null,null,"a"), new(2,Equity,1,0,credit,null,null,"b")];
        Action validate = () => LedgerPostingModel.ValidateBalanced(lines);
        if (valid) validate.Should().NotThrow(); else validate.Should().Throw<FinancialOperationException>().Which.Code.Should().Be(FinancialReasons.UnbalancedJournal);
    }

    [Theory]
    [InlineData(LedgerTransactionKind.DepositConfirmed)]
    [InlineData(LedgerTransactionKind.FundTransfer)]
    [InlineData(LedgerTransactionKind.Commission)]
    public void Configured_rule_generates_balanced_entries(LedgerTransactionKind kind)
    {
        var (request, rule) = Posting(kind);
        var plan = LedgerPostingModel.Calculate(request, rule);
        plan.Lines.Should().HaveCount(2);
        plan.Lines[0].Debit.Should().Be(100);
        plan.Lines[1].Credit.Should().Be(100);
        if (kind == LedgerTransactionKind.FundTransfer)
        { plan.Lines[0].FundId.Should().Be(2); plan.Lines[1].FundId.Should().Be(1); }
    }

    [Theory]
    [InlineData(LedgerTransactionKind.WithdrawalRequested,100)]
    [InlineData(LedgerTransactionKind.WithdrawalCancelled,-100)]
    public void Pending_withdrawal_changes_obligation_without_fictitious_cash_journal(LedgerTransactionKind kind, decimal delta)
    {
        var (request, rule) = Posting(kind);
        var plan = LedgerPostingModel.Calculate(request, rule);
        plan.Lines.Should().BeEmpty(); plan.WithdrawalObligationDelta.Should().Be(delta);
    }

    [Fact]
    public void Realization_clears_prior_unrealized_before_recognizing_realized_profit()
    {
        var (request, rule) = Posting(LedgerTransactionKind.RealizedPnl);
        rule = rule with { ValuationAsset = new(103,1), UnrealizedPnl = new(104,1) };
        var plan = LedgerPostingModel.Calculate(request, rule, previousUnrealized: 60);
        plan.Lines.Should().HaveCount(4);
        plan.Lines[0].Account.AccountId.Should().Be(104); plan.Lines[0].Debit.Should().Be(60);
        plan.Lines[1].Account.AccountId.Should().Be(103); plan.Lines[1].Credit.Should().Be(60);
        plan.Lines[2].Debit.Should().Be(100); plan.Lines[3].Credit.Should().Be(100);
    }

    [Theory]
    [InlineData("CAD",100)]
    [InlineData("USD",100.001)]
    public void Unsupported_currency_and_implicit_rounding_are_rejected(string currency, decimal amount)
    {
        var (request, rule) = Posting(LedgerTransactionKind.DepositConfirmed);
        FluentActions.Invoking(() => LedgerPostingModel.Calculate(request with { Currency=currency, Amount=amount },rule))
            .Should().Throw<FinancialOperationException>();
    }

    [Fact]
    public void Withdrawal_cannot_accept_caller_authored_account_lines()
    {
        var (request, rule) = Posting(LedgerTransactionKind.WithdrawalRequested);
        request = request with { Lines=[new() { AccountId=101, Amount=100, Currency="USD", PostingSide=PostingSide.Debit }] };
        FluentActions.Invoking(() => LedgerPostingModel.Calculate(request,rule)).Should().Throw<FinancialOperationException>()
            .Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
    }

    [Fact]
    public void Lifecycle_command_cannot_consume_a_reservation()
    {
        var (state, request) = Reservation(CapacityChangeKind.Consume);
        FluentActions.Invoking(() => CapacityLifecycleModel.Apply(state, request, false, DateTime.UtcNow))
            .Should().Throw<FinancialOperationException>().Which.Code.Should().Be(FinancialReasons.InvalidLifecycle);
    }

    [Theory]
    [InlineData(CapacityChangeKind.ExpireUnconsumed)]
    [InlineData(CapacityChangeKind.ReleaseUnconsumed)]
    public void Consumed_commitment_cannot_be_released_by_expiry_or_unconsumed_release(CapacityChangeKind kind)
    {
        var (state, request) = Reservation(kind);
        state = state with { Status=ReservationStatus.Consumed, ExecutionId=request.ExecutionId, ValidUntilUtc=DateTime.UtcNow.AddMinutes(-1) };
        request = request with { CancelledUnits=10, RemainingUnits=0 };
        FluentActions.Invoking(() => CapacityLifecycleModel.Apply(state, request, false, DateTime.UtcNow)).Should().Throw<FinancialOperationException>();
    }

    [Fact]
    public void Partial_fill_and_reconciled_cancel_retain_only_filled_position_usage()
    {
        var (state, request) = Reservation(CapacityChangeKind.RecordFill);
        state = state with { Status=ReservationStatus.Working, ExecutionId=request.ExecutionId };
        request = request with { FilledUnits=4, RemainingUnits=6 };
        var fill = CapacityLifecycleModel.Apply(state, request, false, DateTime.UtcNow);
        fill.HeldFraction.Should().Be(0); fill.WorkingFraction.Should().Be(0.6m); fill.PositionFraction.Should().Be(0.4m);
        state = state with { Status=ReservationStatus.PartiallyFilled,FilledUnits=4,RemainingUnits=6,Version=2 };
        request = request with { ChangeKind=CapacityChangeKind.ConfirmCancel,ExpectedReservationVersion=2,CancelledUnits=6,RemainingUnits=0,RelatedPostingReference="reconciled-source" };
        var cancel = CapacityLifecycleModel.Apply(state,request,false,DateTime.UtcNow);
        cancel.WorkingFraction.Should().Be(0); cancel.PositionFraction.Should().Be(0.4m); cancel.Status.Should().Be(ReservationStatus.Filled);
    }

    [Fact]
    public void Semantic_hash_normalizes_decimal_scale_and_object_property_order()
    {
        FinancialCanonicalHash.Compute(new { Amount=1.00m, Currency="USD" }).Should().Be(
            FinancialCanonicalHash.Compute(new { Currency="USD", Amount=1m }));
        FinancialCanonicalHash.Compute(new { Amount=1m }).Should().NotBe(FinancialCanonicalHash.Compute(new { Amount=2m }));
    }

    [Fact]
    public void Financial_request_fingerprint_is_stable_after_standard_messagepack_normalizes_default_timestamps()
    {
        var request=new PostFundTransactionCommand { OperationId=Guid.NewGuid(),RequestedAtUtc=DateTime.UtcNow,
            Body=new() { AccountingDate=new(2026,9,8),Amount=100m } };
        var bytes=TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(request)!;
        var restored=TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Deserialize<PostFundTransactionCommand>(bytes)!;
        FinancialCanonicalHash.Request(restored).Should().Be(FinancialCanonicalHash.Request(request));
    }

    [Fact]
    public void Unchanged_valuation_has_a_source_fact_without_fictitious_zero_journal_lines()
    {
        var (request,rule)=Posting(LedgerTransactionKind.Valuation);
        var result=LedgerPostingModel.Calculate(request,rule,previousUnrealized:100);
        result.Lines.Should().BeEmpty(); result.IsActualFinancialFact.Should().BeTrue();
        result.WithdrawalObligationDelta.Should().Be(0);
    }

    [Theory]
    [InlineData(LedgerTransactionKind.Adjustment)]
    [InlineData(LedgerTransactionKind.OpeningBalance)]
    public void Privileged_postings_require_permission_even_without_raw_lines(LedgerTransactionKind kind)
    {
        var (request,rule)=Posting(kind);
        FluentActions.Invoking(()=>LedgerPostingModel.Calculate(request,rule)).Should().Throw<FinancialOperationException>()
            .Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        LedgerPostingModel.Calculate(request,rule,allowRawAdjustment:true).Lines.Should().HaveCount(2);
    }

    [Fact]
    public void Hashed_requirements_cannot_omit_the_portfolio_funding_exposure()
    {
        var requirements=new CapacityRequirements { SettlementCash=50,MarginFunding=20,FeeReserve=1,VariationReserve=2,
            LossCharge=100,MarginRequirement=20,GrossNotional=1000,GrossContracts=2,PositionSlots=1 };
        requirements=requirements with { Exposures=[
            new() { ScopeKind=CapacityScopeKind.Portfolio,ScopeKey="1",Measure=CapacityMeasure.SettlementCash,Unit=CapacityUnit.Usd,Amount=73 },
            new() { ScopeKind=CapacityScopeKind.Portfolio,ScopeKey="1",Measure=CapacityMeasure.LossCharge,Unit=CapacityUnit.Usd,Amount=100 },
            new() { ScopeKind=CapacityScopeKind.Portfolio,ScopeKey="1",Measure=CapacityMeasure.Margin,Unit=CapacityUnit.Usd,Amount=20 },
            new() { ScopeKind=CapacityScopeKind.Portfolio,ScopeKey="1",Measure=CapacityMeasure.GrossNotional,Unit=CapacityUnit.Usd,Amount=1000 },
            new() { ScopeKind=CapacityScopeKind.Portfolio,ScopeKey="1",Measure=CapacityMeasure.PositionSlots,Unit=CapacityUnit.Positions,Amount=1 }] };
        CapacityAdmissionModel.ValidateScopeVector(requirements,CapacityScopeKind.Portfolio,"1",false);
        var incomplete=requirements with { Exposures=requirements.Exposures.Skip(1).ToArray() };
        FluentActions.Invoking(()=>CapacityAdmissionModel.ValidateScopeVector(incomplete,CapacityScopeKind.Portfolio,"1",false))
            .Should().Throw<FinancialOperationException>().Which.Code.Should().Be(FinancialReasons.InvalidContract);
    }

    static (LedgerPostingRequest,LedgerPostingRule) Posting(LedgerTransactionKind kind)
    {
        var rule = new LedgerPostingRule(Guid.NewGuid(),1,"rule-hash",kind,Cash,Equity,false);
        return (new() { BookId=1,FundId=1,CounterpartyFundId=2,Currency="USD",Amount=100,TransactionKind=kind,
            RelatedObligationId=Guid.NewGuid(),PostingRule=new() { RuleId=rule.RuleId,Version=1,ContentHash="rule-hash" } },rule);
    }
    static (ReservationSnapshot,CapacityLifecycleRequest) Reservation(CapacityChangeKind kind)
    {
        var id=Guid.NewGuid();
        return (new(id,1,ReservationStatus.Reserved,10,0,0,10,"requirements",DateTime.UtcNow.AddMinutes(1),null,0),
            new() { ReservationId=id,ExpectedReservationVersion=1,ChangeKind=kind,RemainingUnits=10,ExpectedRequirementsHash="requirements",ExecutionId=Guid.NewGuid() });
    }
}
