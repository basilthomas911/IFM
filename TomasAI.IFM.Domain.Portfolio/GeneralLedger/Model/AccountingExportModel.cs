using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Maps posted journals without changing signs, amounts or identities. Corrections remain separate linked journals.</summary>
public static class AccountingExportModel
{
    public static AccountingExportPayload Create(AccountingExportRequest request,IReadOnlyList<AccountingJournalSource> sources)
    {
        ValidateRequest(request);
        Require(sources.Count==request.JournalIds.Length && sources.Select(x=>x.Journal.JournalId).Order()
            .SequenceEqual(request.JournalIds.Order()),"Export source set differs from the requested committed cut.");
        var mapping=request.Mapping.Accounts.ToDictionary(x=>(x.AccountId,x.AccountVersion));
        var journals=new List<AccountingExportJournal>();
        foreach(var source in sources.OrderBy(x=>x.FinancialRevision).ThenBy(x=>x.Journal.JournalId))
        {
            var journal=source.Journal;
            Require(journal.BookId==request.BookId && source.FinancialRevision>0 && source.FinancialRevision<=request.SourceRevision
                && !string.IsNullOrWhiteSpace(journal.JournalHash) && journal.Entries.Length is >=2 and <=256,
                "Journal is outside this book/cut or has invalid source metadata.");
            Require(journal.Entries.Select(x=>x.Ordinal).Distinct().Count()==journal.Entries.Length
                && journal.Entries.All(x=>x.Ordinal>0 && (x.Debit>0 && x.Credit==0 || x.Credit>0 && x.Debit==0)
                    && decimal.Round(x.Debit,2)==x.Debit && decimal.Round(x.Credit,2)==x.Credit)
                && journal.Entries.Sum(x=>x.Debit)==journal.Entries.Sum(x=>x.Credit),"Export journal is not balanced.");
            var lines=journal.Entries.OrderBy(x=>x.Ordinal).Select(line=>
            {
                Require(mapping.TryGetValue((line.AccountId,line.AccountVersion),out var account),"Exact account version lacks an external mapping.");
                return new AccountingExportLine(line.Ordinal,account!.ExternalAccountReference,line.FundId,line.Debit,line.Credit);
            }).ToArray();
            journals.Add(new(journal.JournalId,journal.JournalHash,journal.AccountingDate,source.ReversesJournalId,lines));
        }
        return new(request.ExportId,request.PortfolioId,request.BookId,request.Mapping.DestinationCompany,request.SourceRevision,
            request.Mapping.Version,FinancialCanonicalHash.Compute(request.Mapping with
                { Accounts=request.Mapping.Accounts.OrderBy(x=>x.AccountId).ThenBy(x=>x.AccountVersion).ToArray() }),journals.ToArray());
    }
    public static void ValidateRequest(AccountingExportRequest request)
    {
        Require(request.ExportId!=Guid.Empty && request.PortfolioId>0 && request.BookId>0 && request.SourceRevision>0
            && request.JournalIds.Length is >=1 and <=100 && request.JournalIds.All(x=>x>0)
            && request.JournalIds.Distinct().Count()==request.JournalIds.Length,"Invalid bounded export request.");
        var mapping=request.Mapping;
        Require(mapping is not null && !string.IsNullOrWhiteSpace(mapping.DestinationCompany) && mapping.DestinationCompany.Length<=128
            && mapping.Version>0 && mapping.Accounts.Length is >=1 and <=256
            && mapping.Accounts.All(x=>x.AccountId>0 && x.AccountVersion>0 && !string.IsNullOrWhiteSpace(x.ExternalAccountReference)
                && x.ExternalAccountReference.Length<=128)
            && mapping.Accounts.Select(x=>(x.AccountId,x.AccountVersion)).Distinct().Count()==mapping.Accounts.Length,"Invalid export mapping.");
    }
    static void Require(bool valid,string message) { if(!valid) throw new ArgumentException(message); }
}
