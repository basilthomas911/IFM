using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

public static class LedgerConfigurationValidation
{
    public static List<ValidationError> ValidateLedgerConfiguration(this List<ValidationError> errors,ConfigureLedgerCommand request)
    {
        errors.ValidateFinancialRequest<ConfigureLedgerCommand,LedgerConfigurationRequest>(request,ActorType.Command,ConfigureLedgerCommand.Actor,ConfigureLedgerCommand.Verb);
        if(request.Body is not { } body) return errors;
        Add(Enum.IsDefined(body.Action) && body.Action!=LedgerConfigurationAction.Undefined,"A supported configuration action is required.");
        Add(body.BookId>0 && body.ExpectedVersion>=0,"Book and version must be valid.");
        Add(!string.IsNullOrWhiteSpace(body.Reason) && body.Reason.Length<=1024,"A bounded audit reason is required.");
        Add(body.Accounts is { Length:<=128 } && body.Rules is { Length:<=128 },"Configuration lists must be bounded.");
        if(errors.Count>0) return errors;
        if(body.Action is LedgerConfigurationAction.CreateBook or LedgerConfigurationAction.OpenPeriod)
            Add(body.PeriodId!=Guid.Empty && body.PeriodStart!=default && body.PeriodEnd>=body.PeriodStart,"An identified valid period is required.");
        if(body.Action is LedgerConfigurationAction.ClosePeriod or LedgerConfigurationAction.ReopenPeriod)
            Add(body.PeriodId!=Guid.Empty && body.ExpectedVersion>0,"Exact period identity/version is required.");
        if(body.Action is LedgerConfigurationAction.CreateBook or LedgerConfigurationAction.RefreshAuthority or LedgerConfigurationAction.QualifyDevelopmentBook)
        {
            Add(body.Book is not null && body.Book.BookId==body.BookId && body.Book.PortfolioId==request.PortfolioId,"Book scope must match the request.");
            if(body.Book is { } book)
            {
                Add(book.Funds.Length is >0 and <=128 && book.Funds.Select(x=>x.FundId).Distinct().Count()==book.Funds.Length,"Unique bounded Fund membership is required.");
                Add(book.Currency=="USD" && book.AccountingEntityId!=Guid.Empty && !string.IsNullOrWhiteSpace(book.Environment) && !string.IsNullOrWhiteSpace(book.ExecutionAccountReference),"Explicit USD accounting ownership is required.");
            }
        }
        if(body.Action is LedgerConfigurationAction.CreateBook or LedgerConfigurationAction.AddAccountVersion or LedgerConfigurationAction.RetireAccount)
            Add(body.Accounts.Length>0,"Account versions are required.");
        if(body.Action is LedgerConfigurationAction.CreateBook or LedgerConfigurationAction.AddPostingRuleVersion or LedgerConfigurationAction.RetirePostingRule)
            Add(body.Rules.Length>0,"Posting rule versions are required.");
        Add(body.Accounts.Select(x=>(x.AccountId,x.Version)).Distinct().Count()==body.Accounts.Length,"Duplicate account versions are prohibited.");
        Add(body.Rules.Select(x=>(x.RuleId,x.Version)).Distinct().Count()==body.Rules.Length,"Duplicate rule versions are prohibited.");
        foreach(var account in body.Accounts)
        {
            Add(account.AccountId>0 && account.Version>0 && account.NormalSide is PostingSide.Debit or PostingSide.Credit,"Invalid account version.");
            Add(account.Category is "Cash" or "Equity" or "Liability" or "Asset" or "Expense" or "Revenue" or "UnrealizedPnl" or "RealizedPnl","Unsupported account category.");
            Add(account.ContentHash==FinancialCanonicalHash.Compute(account with { ContentHash=string.Empty }),"Account fingerprint does not match its definition.");
        }
        foreach(var rule in body.Rules)
        {
            Add(rule.RuleId!=Guid.Empty && rule.Version>0 && Enum.IsDefined(rule.Kind) && rule.Kind!=LedgerTransactionKind.Undefined,"Invalid rule version.");
            Add(rule.Debit is { AccountId:>0,Version:>0 } && rule.Credit is { AccountId:>0,Version:>0 },"Exact account bindings are required.");
            Add(rule.ContentHash==FinancialCanonicalHash.Compute(rule with { ContentHash=string.Empty }),"Rule fingerprint does not match its definition.");
            if(body.Action==LedgerConfigurationAction.CreateBook)
                foreach(var binding in new[] { rule.Debit,rule.Credit,rule.ValuationAsset,rule.UnrealizedPnl }.OfType<LedgerAccountBinding>())
                    Add(body.Accounts.Any(x=>x.AccountId==binding.AccountId && x.Version==binding.Version),"Initial rule references an account outside this book.");
        }
        if(body.Action==LedgerConfigurationAction.QualifyDevelopmentBook)
            Add(body.ReconciliationId is not null && body.Book is { MigrationQualified:false,Environment:"Emulator" } && body.Book.Funds.All(x=>!x.CanSpend),"Development qualification requires reconciliation and an unqualified non-spending book.");
        if(body.Action is LedgerConfigurationAction.Reconcile or LedgerConfigurationAction.ClosePeriod or LedgerConfigurationAction.RefreshAuthority or LedgerConfigurationAction.QualifyDevelopmentBook)
            Add(!string.IsNullOrWhiteSpace(body.SourceCut),"A source watermark is required.");
        return errors;
        void Add(bool valid,string message) { if(!valid) errors.Add(new(FinancialReasons.InvalidContract.ToString(System.Globalization.CultureInfo.InvariantCulture),message)); }
    }
}
