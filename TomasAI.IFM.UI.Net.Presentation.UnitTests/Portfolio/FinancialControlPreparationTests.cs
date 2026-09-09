using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Portfolio;

[Trait("Category","PortfolioFinancial")]
public sealed class FinancialControlPreparationTests
{
    static readonly FinancialReadScope Scope=new() { PortfolioId=1,Access=new("test",["PortfolioAdministrator"]) };
    static readonly DateTime Now=new(2026,9,9,0,0,0,DateTimeKind.Utc);
    static readonly Guid PeriodId=Guid.NewGuid();
    [Fact]
    public void Book_command_preserves_prepared_business_keys_and_does_not_enable_spending()
    {
        var draft=new LedgerConfigurationRequest { Action=LedgerConfigurationAction.CreateBook,BookId=12,
            Book=new() { BookId=12,PortfolioId=1,Environment="Emulator",Funds=[new() { FundId=2 }] },Accounts=[new(99,1,"Cash",PostingSide.Debit,true,"")] };
        var command=FinancialControlPreparation.CreateBook(Scope,draft,"Reviewed",Now);
        command.Body.Accounts.Single().AccountId.Should().Be(99);command.Body.BookId.Should().Be(12);
        command.ExpectedFinancialRevision.Should().Be(0);command.Body.Book!.MigrationQualified.Should().BeFalse();
        command.OperationId.Should().Be(command.CommandId);command.InputSha256.Should().Be(FinancialCanonicalHash.Request(command));
        Action spending=()=>FinancialControlPreparation.CreateBook(Scope,draft with { Book=draft.Book with { MigrationQualified=true } },"Reviewed",Now);
        spending.Should().Throw<ArgumentException>();
        Action wrongPortfolio=()=>FinancialControlPreparation.CreateBook(Scope with { PortfolioId=2 },draft,"Reviewed",Now);
        wrongPortfolio.Should().Throw<ArgumentException>();
    }
    [Fact]
    public void Closing_freezes_configured_period_version_reconciliation_and_financial_revision()
    {
        var snapshot=Snapshot();
        var command=FinancialControlPreparation.Create(Scope,snapshot,LedgerConfigurationAction.ClosePeriod,"Month end",Now,periodId:PeriodId);
        command.Body.PeriodId.Should().Be(PeriodId);command.Body.ExpectedVersion.Should().Be(3);
        command.Body.ReconciliationId.Should().Be(snapshot.Value!.LatestReconciliation!.ReconciliationId);
        command.Body.SourceCut.Should().Be("verified-ledger:4");command.ExpectedFinancialRevision.Should().Be(5);
        command.OperationId.Should().Be(command.CommandId);command.InputSha256.Should().Be(FinancialCanonicalHash.Request(command));
    }
    [Fact]
    public void New_period_has_generated_identity_and_rejects_overlap()
    {
        var command=FinancialControlPreparation.Create(Scope,Snapshot(),LedgerConfigurationAction.OpenPeriod,"Next year",Now,start:new(2027,1,1),end:new(2027,12,31));
        command.Body.PeriodId.Should().NotBeEmpty();command.Body.PeriodId.Should().NotBe(PeriodId);
        Action overlap=()=>FinancialControlPreparation.Create(Scope,Snapshot(),LedgerConfigurationAction.OpenPeriod,"Overlap",Now,start:new(2026,2,1),end:new(2027,1,1));
        overlap.Should().Throw<ArgumentException>();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Missing_or_mismatched_reconciliation_cannot_prepare_close(bool mismatch)
    {
        var snapshot=Snapshot();snapshot=snapshot with { Value=snapshot.Value! with
        { LatestReconciliation=mismatch?snapshot.Value.LatestReconciliation! with { Credits=99 }:null } };
        Action action=()=>FinancialControlPreparation.Create(Scope,snapshot,LedgerConfigurationAction.ClosePeriod,"Close",Now,periodId:PeriodId);
        action.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void Reader_cannot_prepare_a_control_command()
    {
        Action action=()=>FinancialControlPreparation.Create(Scope with { Access=new("reader",["LedgerRead"],[1]) },Snapshot(),LedgerConfigurationAction.Reconcile,"Check",Now);
        action.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void Reopen_requires_additional_permission_and_an_existing_closed_period()
    {
        var snapshot=Snapshot();snapshot=snapshot with { Value=snapshot.Value! with { Periods=[snapshot.Value.Periods[0] with { State="Closed" }] } };
        var scope=Scope with { Access=new("operator",["LedgerConfigure"],[1]) };
        Action missing=()=>FinancialControlPreparation.Create(scope,snapshot,LedgerConfigurationAction.ReopenPeriod,"Correction",Now,periodId:PeriodId);
        missing.Should().Throw<InvalidOperationException>();
        var command=FinancialControlPreparation.Create(scope with { Access=new("operator",["LedgerConfigure","LedgerPeriodReopen"],[1]) },snapshot,
            LedgerConfigurationAction.ReopenPeriod,"Correction",Now,periodId:PeriodId);
        command.Body.ExpectedVersion.Should().Be(3);
    }
    static FinancialRead<FinancialLedgerConfiguration> Snapshot()=>new(FinancialReadStatus.Found,
        new(10,"USD","Emulator","Active","source:1",[new(PeriodId,new(2026,1,1),new(2026,12,31),3,"Open")],[],[],
            new(Guid.NewGuid(),4,1,2,100,100,[],"verified-ledger:4",new('A',64))),5,Now);

    [Fact]
    public void Editing_uses_exact_active_versions_and_does_not_rewrite_existing_definitions()
    {
        var cash=new LedgerAccountDefinition(20,2,"Cash",PostingSide.Debit,true,"");
        var equity=new LedgerAccountDefinition(21,1,"Equity",PostingSide.Credit,true,"");
        var rule=new LedgerPostingRule(Guid.NewGuid(),3,"",LedgerTransactionKind.DepositConfirmed,new(20,2),new(21,1),true);
        var snapshot=Snapshot();snapshot=snapshot with { Value=snapshot.Value! with {
            Accounts=[new(cash,"Active"),new(equity,"Active")],Rules=[new(rule,"Active",new(2026,1,1),null)] } };
        var accountEdit=FinancialControlPreparation.EditAccount(Scope,snapshot,20,PostingSide.Debit,false,"Dimension review",Now);
        accountEdit.Body.ExpectedVersion.Should().Be(2);accountEdit.Body.Accounts.Single().Version.Should().Be(3);
        accountEdit.Body.Accounts.Single().Category.Should().Be("Cash");cash.FundDimensionRequired.Should().BeTrue();
        var ruleEdit=FinancialControlPreparation.EditRule(Scope,snapshot,rule.RuleId,new(20,2),new(21,1),true,null,null,new(2026,9,9),"Rule review",Now);
        ruleEdit.Body.ExpectedVersion.Should().Be(3);ruleEdit.Body.Rules.Single().Version.Should().Be(4);
        ruleEdit.ExpectedFinancialRevision.Should().Be(snapshot.FinancialRevision);
        ruleEdit.InputSha256.Should().Be(FinancialCanonicalHash.Request(ruleEdit));
        Action stale=()=>FinancialControlPreparation.EditRule(Scope,snapshot,rule.RuleId,new(20,1),new(21,1),true,null,null,new(2026,9,9),"Stale",Now);
        stale.Should().Throw<ArgumentException>();
        Action unpaired=()=>FinancialControlPreparation.EditRule(Scope,snapshot,rule.RuleId,new(20,2),new(21,1),true,new(20,2),null,new(2026,9,9),"Unpaired",Now);
        unpaired.Should().Throw<ArgumentException>();
    }
}
