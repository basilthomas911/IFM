using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

/// <summary>Prepares development book configuration from committed membership, with sequence-generated keys and no money effects.</summary>
public sealed class FinancialBookPreparation(IPortfolioEventStore sources,IPortfolioFinancialDbContext database,
    FinancialIdentityAllocator identities,FinancialDevelopmentPolicy development)
{
    public async Task<FinancialRead<FinancialBookSetup>> PrepareAsync(FinancialReadScope scope,PrepareFinancialBookRequest request,CancellationToken token)
    {
        var admin=scope.Access?.Roles?.Contains("PortfolioAdministrator")==true;
        Require(scope.PortfolioId>0 && scope.FundId is null && !string.IsNullOrWhiteSpace(scope.Access?.Principal) &&
            (admin || scope.Access?.Roles?.Contains("LedgerConfigure")==true && scope.Access.PortfolioIds?.Contains(scope.PortfolioId)==true),"Portfolio ledger configuration permission is required.");
        Require(development.IsDevelopmentEnvironment,"Development book preparation requires the Development host environment.");
        Require(await database.ReadBookAsync(scope.PortfolioId,token) is null,"A ledger book already exists for this Portfolio.");
        var portfolio=await sources.LoadPortfolioAsync(new(scope.PortfolioId),token);
        Require(portfolio.Current is not null && !portfolio.IsDeleted && portfolio.Current.BaseCurrency=="USD","An existing USD Portfolio is required.");
        var fundAuthorities=new List<FinancialFundAuthority>();var names=new List<string>();
        Require(portfolio.FundIds.Count is >0 and <=128,"A book requires between one and 128 current Funds.");
        foreach(var id in portfolio.FundIds.Order())
        {
            var fund=await sources.LoadFundAsync(new(scope.PortfolioId,id),token);
            // Historical imported Funds stay read-only and do not become spending authorities.
            if(fund.Current is null || fund.Current.IsLegacyHistory) continue;
            fundAuthorities.Add(new() { FundId=id,CanSpend=false,PortfolioStreamVersion=portfolio.Revision,FundStreamVersion=fund.Revision });
            names.Add(fund.Current.Name);
        }
        Require(fundAuthorities.Count>0,"Create a current Fund before setting up this book; historical Funds remain read-only.");
        var accounts=portfolio.Current!.BrokerAccountRefs.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if(string.IsNullOrEmpty(request.ExecutionAccountReference)) return Result(new(accounts,names.ToArray(),null));
        Require(accounts.Contains(request.ExecutionAccountReference,StringComparer.Ordinal),"Select an execution account configured on this Portfolio.");
        Require(request.PeriodStart!=default && request.PeriodEnd>=request.PeriodStart,"Valid initial accounting period dates are required.");
        var bookId=await identities.BookAsync(token);
        var chart=new List<LedgerAccountDefinition>();
        foreach(var (category,side) in new[] { ("Cash",PostingSide.Debit),("Equity",PostingSide.Credit),("Expense",PostingSide.Debit),
            ("Asset",PostingSide.Debit),("UnrealizedPnl",PostingSide.Credit),("RealizedPnl",PostingSide.Credit) })
        {
            var account=new LedgerAccountDefinition(await identities.AccountAsync(token),1,category,side,true,"");
            chart.Add(account with { ContentHash=FinancialCanonicalHash.Compute(account) });
        }
        LedgerAccountBinding Account(string category)=>new(chart.Single(x=>x.Category==category).AccountId,1);
        var rules=new List<LedgerPostingRule>();
        void Rule(LedgerTransactionKind kind,string debit,string credit,bool movement=false,bool clearValuation=false)
        {
            var rule=new LedgerPostingRule(Guid.NewGuid(),1,"",kind,Account(debit),Account(credit),movement,
                clearValuation?Account("Asset"):null,clearValuation?Account("UnrealizedPnl"):null);
            rules.Add(rule with { ContentHash=FinancialCanonicalHash.Compute(rule) });
        }
        Rule(LedgerTransactionKind.OpeningBalance,"Cash","Equity");
        Rule(LedgerTransactionKind.DepositConfirmed,"Cash","Equity",true);
        Rule(LedgerTransactionKind.WithdrawalRequested,"Equity","Cash");
        Rule(LedgerTransactionKind.WithdrawalCancelled,"Equity","Cash");
        Rule(LedgerTransactionKind.WithdrawalSettled,"Equity","Cash",true);
        Rule(LedgerTransactionKind.FundTransfer,"Cash","Cash");
        Rule(LedgerTransactionKind.Commission,"Expense","Cash",true);
        Rule(LedgerTransactionKind.Valuation,"Asset","UnrealizedPnl");
        Rule(LedgerTransactionKind.RealizedPnl,"Cash","RealizedPnl",true,true);
        Rule(LedgerTransactionKind.Reversal,"Cash","Equity");
        // Settlement and arbitrary adjustment rules require their own economic mapping; no implicit cash/notional rule.
        var draft=new LedgerConfigurationRequest { Action=LedgerConfigurationAction.CreateBook,BookId=bookId,
            Book=new() { BookId=bookId,PortfolioId=scope.PortfolioId,AccountingEntityId=Guid.NewGuid(),Environment="Emulator",
                ExecutionAccountReference=request.ExecutionAccountReference,Funds=fundAuthorities.ToArray(),SourceWatermark=$"DevelopmentSetup:{scope.PortfolioId}:{portfolio.Revision}" },
            Accounts=chart.ToArray(),Rules=rules.ToArray(),PeriodId=Guid.NewGuid(),PeriodStart=request.PeriodStart,PeriodEnd=request.PeriodEnd };
        return Result(new(accounts,names.ToArray(),draft));
        static FinancialRead<FinancialBookSetup> Result(FinancialBookSetup setup)=>new(FinancialReadStatus.Found,setup,0,DateTime.UtcNow);
    }
    static void Require(bool condition,string message)
    { if(!condition) throw new FinancialOperationException(FinancialReasons.AuthorityDenied,message); }
}
