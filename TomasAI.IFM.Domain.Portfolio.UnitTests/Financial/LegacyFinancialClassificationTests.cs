using FluentAssertions;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-06")]
public sealed class LegacyFinancialClassificationTests
{
    [Fact]
    public void Every_declared_legacy_kind_has_an_explicit_rule_and_unknown_values_quarantine()
    {
        LegacyFinancialClassification.TypeRules.Keys.Order().Should().Equal(Enum.GetValues<FundTransactionType>().Order());
        LegacyFinancialClassification.Classify(Source((FundTransactionType)999),Evidence,LedgerImportMode.FullPostedHistory)
            .Disposition.Should().Be(LegacyFinancialDisposition.Quarantined);
    }
    [Theory]
    [InlineData(FundTransactionType.OpeningTrade)]
    [InlineData(FundTransactionType.OpeningTradeAdjustment)]
    [InlineData(FundTransactionType.EndOfDayProcessed)]
    public void Opening_balance_snapshots_and_eod_markers_do_not_invent_cash(FundTransactionType kind)
    {
        var result=LegacyFinancialClassification.Classify(Source(kind),Evidence,LedgerImportMode.FullPostedHistory);
        result.Disposition.Should().Be(LegacyFinancialDisposition.HistoricalOnly); result.PostingAmount.Should().BeNull();
    }
    [Theory]
    [InlineData(FundTransactionType.CashDeposit,100)]
    [InlineData(FundTransactionType.CashWithdrawal,-100)]
    public void Confirmed_historical_cash_sign_is_explicit_and_never_taken_from_legacy_balance(FundTransactionType kind,decimal amount)
    {
        var source=Source(kind) with { Balance=987654321m };
        LegacyFinancialClassification.Classify(source,Evidence,LedgerImportMode.FullPostedHistory).PostingAmount.Should().Be(amount);
        LegacyFinancialClassification.Classify(source,Evidence with { CashMovementConfirmed=false },LedgerImportMode.FullPostedHistory)
            .Disposition.Should().Be(LegacyFinancialDisposition.Quarantined);
    }
    [Fact]
    public void Opening_plus_history_mode_never_reposts_pre_cut_cash()
    {
        var result=LegacyFinancialClassification.Classify(Source(FundTransactionType.CashDeposit),Evidence,LedgerImportMode.OpeningBalanceWithHistory);
        result.Disposition.Should().Be(LegacyFinancialDisposition.HistoricalOnly); result.PostingAmount.Should().BeNull();
    }
    [Fact]
    public void Missing_currency_and_unlinked_corrections_quarantine()
    {
        LegacyFinancialClassification.Classify(Source(FundTransactionType.CashDeposit),Evidence with { Currency="" },LedgerImportMode.FullPostedHistory)
            .Reason.Should().Be("LEGACY.CURRENCY.UNQUALIFIED");
        LegacyFinancialClassification.Classify(Source(FundTransactionType.CashWithdrawalAdjustment),Evidence,LedgerImportMode.FullPostedHistory)
            .Disposition.Should().Be(LegacyFinancialDisposition.Quarantined);
    }
    static readonly LegacyFinancialEvidence Evidence=new("USD",true,true,true,null,"qualified-source-fixture");
    static FundTransactionReadModel Source(FundTransactionType kind)=>new(1,new DateTime(2026,9,8,12,0,0,DateTimeKind.Utc),kind,1,2,3,default,new(2026,9,8),TradeStatus.Open,"fixture",100,100);
}
