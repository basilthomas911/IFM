using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Builds a version-bound ledger control request from configured selectors, never user-entered business keys.</summary>
public static class FinancialControlPreparation
{
    public static ConfigureLedgerCommand RefreshAuthority(FinancialReadScope scope,FinancialRead<FinancialAuthorityDraft> snapshot,string reason,DateTime now)
    {
        var draft=snapshot.Value?.Draft;
        if(snapshot.Status!=FinancialReadStatus.Found || draft?.Action!=LedgerConfigurationAction.RefreshAuthority || draft.Book?.PortfolioId!=scope.PortfolioId ||
            scope.FundId is not null || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length>1024 || now.Kind!=DateTimeKind.Utc ||
            draft.Book.Funds.SelectMany(x=>x.Deployments).Any(x=>x.Reference.ValidUntilUtc<=now))
            throw new ArgumentException("A current, unexpired authority draft and an audit reason are required.");
        if(!scope.Access.Roles.Contains("PortfolioAdministrator") && !scope.Access.Roles.Contains("LedgerConfigure"))
            throw new InvalidOperationException("Ledger configuration permission is required.");
        return Command(scope,snapshot.FinancialRevision,draft with { Reason=reason.Trim() },now);
    }
    public static ConfigureLedgerCommand CreateBook(FinancialReadScope scope,LedgerConfigurationRequest draft,string reason,DateTime now)
    {
        if(scope.FundId is not null || draft.Action!=LedgerConfigurationAction.CreateBook || draft.Book is not { MigrationQualified:false,Environment:"Emulator" } book ||
            book.PortfolioId!=scope.PortfolioId || book.BookId!=draft.BookId || book.Funds.Length==0 || book.Funds.Any(x=>x.CanSpend) ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length>1024 || now.Kind!=DateTimeKind.Utc)
            throw new ArgumentException("A prepared development book and an audit reason are required.");
        if(!scope.Access.Roles.Contains("PortfolioAdministrator") && !scope.Access.Roles.Contains("LedgerConfigure"))
            throw new InvalidOperationException("Ledger configuration permission is required.");
        return Command(scope,0,draft with { Reason=reason.Trim() },now);
    }
    public static ConfigureLedgerCommand Create(FinancialReadScope scope,FinancialRead<FinancialLedgerConfiguration> snapshot,
        LedgerConfigurationAction action,string reason,DateTime now,Guid? periodId=null,DateOnly start=default,DateOnly end=default,
        int? accountId=null,Guid? ruleId=null)
    {
        var configuration=snapshot.Value;
        if(scope.PortfolioId<=0 || scope.FundId is not null || snapshot.Status!=FinancialReadStatus.Found || configuration is null ||
            now.Kind!=DateTimeKind.Utc || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length>1024)
            throw new ArgumentException("Current Portfolio ledger configuration and an audit reason are required.");
        var admin=scope.Access.Roles.Contains("PortfolioAdministrator");
        if(!admin && !scope.Access.Roles.Contains("LedgerConfigure")) throw new InvalidOperationException("Ledger configuration permission is required.");
        var body=new LedgerConfigurationRequest { Action=action,BookId=configuration.BookId,Reason=reason.Trim(),
            SourceCut=$"LedgerSnapshot:{scope.PortfolioId}:{snapshot.FinancialRevision}" };
        switch(action)
        {
            case LedgerConfigurationAction.Reconcile:break;
            case LedgerConfigurationAction.QualifyDevelopmentBook:
                var prepared=configuration.DevelopmentQualificationBook;
                var reconciliation=configuration.LatestReconciliation;
                if(prepared is null || reconciliation is null || reconciliation.Differences.Length!=0 || reconciliation.Debits!=reconciliation.Credits)
                    throw new InvalidOperationException("Record development opening capital and reconcile the unqualified book before qualification.");
                if(!admin && !scope.Access.Roles.Contains("LedgerImport")) throw new InvalidOperationException("Development qualification permission is required.");
                body=body with { Book=prepared,ReconciliationId=reconciliation.ReconciliationId,SourceCut=reconciliation.SourceCut };break;
            case LedgerConfigurationAction.OpenPeriod:
                if(start==default || end<start || configuration.Periods.Any(x=>start<=x.EndDate && end>=x.StartDate))
                    throw new ArgumentException("The new period must have valid dates and must not overlap a configured period.");
                body=body with { PeriodId=Guid.NewGuid(),PeriodStart=start,PeriodEnd=end };break;
            case LedgerConfigurationAction.ClosePeriod:
            case LedgerConfigurationAction.ReopenPeriod:
                var period=configuration.Periods.SingleOrDefault(x=>x.PeriodId==periodId)
                    ??throw new ArgumentException("Select a configured period.");
                var closing=action==LedgerConfigurationAction.ClosePeriod;
                if(period.State!=(closing?"Open":"Closed")) throw new ArgumentException("The selected period is not in the required state.");
                if(!closing && !admin && !scope.Access.Roles.Contains("LedgerPeriodReopen")) throw new InvalidOperationException("Period reopen permission is required.");
                body=body with { PeriodId=period.PeriodId,ExpectedVersion=period.Version,PeriodStart=period.StartDate,PeriodEnd=period.EndDate };
                if(closing)
                {
                    var evidence=configuration.LatestReconciliation;
                    if(evidence is null || evidence.Differences.Length!=0 || evidence.Debits!=evidence.Credits)
                        throw new InvalidOperationException("Run a successful reconciliation before closing a period.");
                    body=body with { ReconciliationId=evidence.ReconciliationId,SourceCut=evidence.SourceCut };
                }
                break;
            case LedgerConfigurationAction.RetireAccount:
                var account=configuration.Accounts.SingleOrDefault(x=>x.Definition.AccountId==accountId && x.State=="Active")
                    ??throw new ArgumentException("Select an active configured account.");
                body=body with { Accounts=[account.Definition],ExpectedVersion=account.Definition.Version };break;
            case LedgerConfigurationAction.RetirePostingRule:
                var rule=configuration.Rules.SingleOrDefault(x=>x.Definition.RuleId==ruleId && x.State=="Active")
                    ??throw new ArgumentException("Select an active configured posting rule.");
                body=body with { Rules=[rule.Definition],ExpectedVersion=rule.Definition.Version };break;
            default:throw new ArgumentException("This ledger control is not supported by this editor.");
        }
        return Command(scope,snapshot.FinancialRevision,body,now);
    }
    static ConfigureLedgerCommand Command(FinancialReadScope scope,long revision,LedgerConfigurationRequest body,DateTime now)
    {
        var operation=Guid.NewGuid();var entity=new LedgerPortfolioId(scope.PortfolioId);
        var command=new ConfigureLedgerCommand { CommandId=operation,OperationId=operation,EntityId=entity,PortfolioId=scope.PortfolioId,
            Subject=new(ActorType.Command,ConfigureLedgerCommand.Actor,ConfigureLedgerCommand.Verb,entity.Format()),
            CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),ExpectedFinancialRevision=revision,
            RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(2),Access=scope.Access,Body=body };
        return command with { InputSha256=FinancialCanonicalHash.Request(command) };
    }
}
