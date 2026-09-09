using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Operator input contains amounts, dates and descriptions; rule/account/business identities come from server selections.</summary>
public sealed record FinancialPostingDraft(LedgerTransactionKind Kind,decimal Amount,DateOnly AccountingDate,
    DateOnly ValueDate,string Description,string MovementReference,bool MovementConfirmed);

public static class FinancialPostingPreparation
{
    public static PostFundTransactionCommand Create(FinancialReadScope scope,FinancialRead<FinancialPostingConfiguration> read,
        LedgerPostingRule rule,FinancialPostingDraft draft,FinancialTransactionRow? related,LedgerPostingReceipt? relatedReceipt,DateTime now)
    {
        var config=read.Value;
        if(read.Status!=FinancialReadStatus.Found || config is null || scope.FundId!=config.FundId || !config.PeriodOpen ||
            config.AccountingDate!=draft.AccountingDate || !config.Rules.Contains(rule) || rule.Kind!=draft.Kind ||
            draft.Amount<=0 || decimal.Round(draft.Amount,2)!=draft.Amount || string.IsNullOrWhiteSpace(draft.Description) ||
            draft.Description.Length>1000 || draft.MovementReference.Length>256 || now.Kind!=DateTimeKind.Utc)
            throw new ArgumentException("An open period, configured posting rule, positive cent amount and description are required.");
        if(rule.RequiresConfirmedMovement && (!draft.MovementConfirmed || string.IsNullOrWhiteSpace(draft.MovementReference)))
            throw new ArgumentException("Confirm the cash movement and supply its reference.");
        var roles=scope.Access.Roles;
        if(!roles.Contains("PortfolioAdministrator") && (!roles.Contains("LedgerPost") || scope.Access.PortfolioIds?.Contains(scope.PortfolioId)!=true))
            throw new UnauthorizedAccessException("Ledger posting permission is required.");
        if(draft.Kind==LedgerTransactionKind.Reversal && !roles.Contains("PortfolioAdministrator") && !roles.Contains("LedgerReverse"))
            throw new UnauthorizedAccessException("Ledger reversal permission is required.");
        if(draft.Kind==LedgerTransactionKind.OpeningBalance && (!config.AllowDevelopmentOpeningCapital ||
            !roles.Contains("PortfolioAdministrator") && !roles.Contains("LedgerImport")))
            throw new UnauthorizedAccessException("Development opening capital is not available for this book or caller.");
        if(draft.Kind is LedgerTransactionKind.Reversal or LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled)
        {
            if(related is null || related.Transaction.FundId!=scope.FundId || relatedReceipt is null ||
                relatedReceipt.OperationId!=related.OperationId || relatedReceipt.FundId!=scope.FundId || relatedReceipt.BookId!=config.BookId)
                throw new ArgumentException("Select the original transaction and its committed receipt.");
            if(draft.Kind==LedgerTransactionKind.Reversal && related.JournalId is not >0)
                throw new ArgumentException("Select a posted journal to reverse.");
            if(draft.Kind!=LedgerTransactionKind.Reversal && (related.Transaction.TransactionKind!=LedgerTransactionKind.WithdrawalRequested || relatedReceipt.ObligationId is null))
                throw new ArgumentException("Select the original withdrawal request.");
        }
        var operation=Guid.NewGuid(); var id=new LedgerPortfolioId(scope.PortfolioId);
        var request=new PostFundTransactionCommand
        {
            CommandId=operation,OperationId=operation,PortfolioId=scope.PortfolioId,EntityId=id,
            Subject=new(ActorType.Command,PostFundTransactionCommand.Actor,PostFundTransactionCommand.Verb,id.Format()),
            CorrelationId=operation,CausationId=operation,RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(2),
            ExpectedFinancialRevision=read.FinancialRevision,Access=scope.Access,
            Body=new()
            {
                BookId=config.BookId,FundId=config.FundId,TransactionKind=draft.Kind,Currency="USD",Amount=draft.Amount,
                AccountingDate=draft.AccountingDate,ValueDate=draft.ValueDate,Description=draft.Description.Trim(),Authority=config.Authority,
                PostingRule=new() { RuleId=rule.RuleId,Version=rule.Version,ContentHash=rule.ContentHash },
                RelatedJournalId=draft.Kind==LedgerTransactionKind.Reversal ? related?.JournalId : null,
                RelatedObligationId=draft.Kind is LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled ? relatedReceipt?.ObligationId : null,
                MovementEvidence=new() { Status=draft.Kind!=LedgerTransactionKind.OpeningBalance && draft.MovementConfirmed?MovementStatus.Confirmed:MovementStatus.Pending,
                    SourceReference=draft.Kind==LedgerTransactionKind.OpeningBalance?string.Empty:draft.MovementReference.Trim(),ObservedAtUtc=now,ReceivedAtUtc=now,ValidUntilUtc=now.AddMinutes(2),
                    ContentHash=FinancialCanonicalHash.Compute(new { draft,Principal=scope.Access.Principal }) },
                Source=new() { System=draft.Kind==LedgerTransactionKind.OpeningBalance?"DevelopmentOpeningCapital":"PortfolioOperator",SourceEntityId=$"{scope.PortfolioId}.{scope.FundId}",SourceEventId=operation,
                    SourceSequence=1,SourceContentHash=FinancialCanonicalHash.Compute(new { draft,Principal=scope.Access.Principal,operation }),OccurredAtUtc=now }
            }
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
}
