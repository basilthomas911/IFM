using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Financial;

[Trait("Category", "PortfolioFinancial")]
public sealed class FinancialPostingScenarios
{
    [Fact]
    public void Given_a_withdrawal_request_when_recorded_then_cash_is_not_reported_as_already_paid()
    {
        var rule = new LedgerPostingRule(Guid.NewGuid(),1,"qualified",LedgerTransactionKind.WithdrawalRequested,new(1,1),new(2,1),false);
        var request = new LedgerPostingRequest { BookId=1,FundId=1,Currency="USD",Amount=700,
            TransactionKind=LedgerTransactionKind.WithdrawalRequested,PostingRule=new() { RuleId=rule.RuleId,Version=1,ContentHash=rule.ContentHash } };
        var result = LedgerPostingModel.Calculate(request,rule);
        result.WithdrawalObligationDelta.Should().Be(700); result.Lines.Should().BeEmpty(); result.IsDiscretionarySpending.Should().BeTrue();
    }

    [Fact]
    public void Given_a_confirmed_loss_when_accounted_then_negative_amount_is_a_real_financial_fact()
    {
        var rule = new LedgerPostingRule(Guid.NewGuid(),1,"qualified",LedgerTransactionKind.TradeSettlement,new(1,1),new(2,1),true);
        var request = new LedgerPostingRequest { BookId=1,FundId=1,Currency="USD",Amount=-1200,
            TransactionKind=LedgerTransactionKind.TradeSettlement,PostingRule=new() { RuleId=rule.RuleId,Version=1,ContentHash=rule.ContentHash },
            MovementEvidence=new() { Status=MovementStatus.Confirmed,SourceReference="emulator-settlement-1" } };
        var result = LedgerPostingModel.Calculate(request,rule);
        result.IsActualFinancialFact.Should().BeTrue(); result.IsDiscretionarySpending.Should().BeFalse();
        result.Lines.Single(x=>x.Account.AccountId==1).Credit.Should().Be(1200);
        result.Lines.Sum(x=>x.Debit-x.Credit).Should().Be(0);
    }
}
