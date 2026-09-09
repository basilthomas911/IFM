using System.Globalization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Internal durable export outbox. Remote delivery changes only its checkpoint, never journals, balances or capacity.</summary>
public sealed class AccountingExportStore(IPostgresEventTransaction transactions)
{
    public Task<AccountingExportReceipt> PrepareAsync(AccountingExportRequest request,FinancialAccess access,
        Func<AccountingExportRequest,IReadOnlyList<AccountingJournalSource>,AccountingExportPayload> build,CancellationToken token=default)
    {
        Authorize(access,request.PortfolioId);
        Require(request.ExportId!=Guid.Empty && request.BookId>0 && request.SourceRevision>0 && request.JournalIds.Length is >=1 and <=100
            && request.JournalIds.Distinct().Count()==request.JournalIds.Length && request.JournalIds.All(x=>x>0)
            && request.Mapping is not null && request.Mapping.Version>0 && !string.IsNullOrWhiteSpace(request.Mapping.DestinationCompany)
            && request.Mapping.DestinationCompany.Length<=128 && request.Mapping.Accounts.Length is >=1 and <=256,
            FinancialReasons.InvalidContract,"Invalid bounded accounting export request.");
        // Freeze the mutable transport arrays before entering an asynchronous database operation.
        request=request with { JournalIds=request.JournalIds.Order().ToArray(),Mapping=request.Mapping with
            { Accounts=request.Mapping.Accounts.OrderBy(x=>x.AccountId).ThenBy(x=>x.AccountVersion).ToArray() } };
        var hash=FinancialCanonicalHash.Compute(request);
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            var authority=await LockAuthorityAsync(db,request.PortfolioId,null,ct);
            var prior=await Read(db,request.Mapping.DestinationCompany,request.ExportId,ct);
            if(prior is not null)
            {
                Require(prior.RequestHash==hash && prior.Payload.PortfolioId==request.PortfolioId,
                    FinancialReasons.SourceConflict,"Export identity was already used with different inputs.");
                return prior;
            }
            Require(authority.Book.BookId==request.BookId && authority.Revision>=request.SourceRevision,
                FinancialReasons.RevisionConflict,"Export is outside this book or its committed source cut.");
            var sources=new List<AccountingJournalSource>();
            foreach(var journalId in request.JournalIds)
            {
                var rows=await db.QueryAsync("""
                    SELECT journal_id,transaction_id,book_id,fund_id,accounting_date,journal_hash,financial_revision,reversal_journal_id
                    FROM portfolio_financial.ledger_journal WHERE portfolio_id=$1 AND book_id=$2 AND journal_id=$3 AND financial_revision<=$4;
                    """,[request.PortfolioId,request.BookId,journalId,request.SourceRevision],r=>new AccountingJournalSource(
                        new(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),r.IsDBNull(3)?null:r.GetInt32(3),r.GetFieldValue<DateOnly>(4),r.GetString(5),[]),
                        r.GetInt64(6),r.IsDBNull(7)?null:r.GetInt64(7)),ct);
                Require(rows.Count==1,FinancialReasons.InvalidContract,"Export journal is missing or outside the exact source cut.");
                var lines=await db.QueryAsync("""
                    SELECT ordinal,account_id,account_version,fund_id,debit,credit,source_line_reference
                    FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal LIMIT 257;
                    """,[journalId],r=>new FinancialJournalEntry(r.GetInt32(0),r.GetInt32(1),r.GetInt64(2),r.IsDBNull(3)?null:r.GetInt32(3),
                        r.GetDecimal(4),r.GetDecimal(5),r.GetString(6)),ct);
                Require(lines.Count is >=2 and <=256,FinancialReasons.InvalidContract,"Export journal line count is invalid.");
                sources.Add(rows[0] with { Journal=rows[0].Journal with { Entries=lines.ToArray() } });
            }
            var payload=build(request,sources);
            Require(payload.ExportId==request.ExportId && payload.PortfolioId==request.PortfolioId && payload.BookId==request.BookId
                && payload.DestinationCompany==request.Mapping.DestinationCompany && payload.SourceRevision==request.SourceRevision,
                FinancialReasons.InvalidContract,"Export builder changed source ownership.");
            var receipt=new AccountingExportReceipt(payload,FinancialCanonicalHash.Compute(payload),hash,"Pending",0,null);
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.accounting_export(destination_company,export_id,source_set,source_cut,payload_hash,mapping_version,delivery_status,metadata)
                VALUES($1,$2,$3,$4,$5,$6,'Pending',$7);
                """,[request.Mapping.DestinationCompany,request.ExportId,Json(request.JournalIds),request.SourceRevision.ToString(CultureInfo.InvariantCulture),
                    receipt.PayloadHash,request.Mapping.Version,Json(receipt)],ct);
            foreach(var source in request.JournalIds)
                await db.ExecuteAsync("""
                    INSERT INTO portfolio_financial.accounting_export_source(destination_company,journal_id,export_id) VALUES($1,$2,$3);
                    """,[request.Mapping.DestinationCompany,source,request.ExportId],ct);
            return receipt;
        },token);
    }

    public Task<AccountingExportReceipt?> ReadAsync(int portfolioId,string company,Guid exportId,FinancialAccess access,CancellationToken token=default)
    {
        Authorize(access,portfolioId);
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            var receipt=await Read(db,company,exportId,ct);
            Require(receipt is null || receipt.Payload.PortfolioId==portfolioId,FinancialReasons.AuthorityDenied,"Export belongs to another Portfolio.");
            return receipt;
        },token);
    }

    /// <summary>Records one stable delivery attempt. Unknown or failed delivery keeps the original payload pending for reconciliation.</summary>
    public Task<AccountingExportReceipt> RecordAttemptAsync(int portfolioId,string company,Guid exportId,Guid attemptId,
        string outcome,string? externalReceipt,FinancialAccess access,CancellationToken token=default)
    {
        Authorize(access,portfolioId);
        Require(attemptId!=Guid.Empty && outcome is "Delivered" or "Failed" or "Unknown"
            && (outcome=="Delivered" ? !string.IsNullOrWhiteSpace(externalReceipt) && externalReceipt.Length<=256 : externalReceipt is null),
            FinancialReasons.InvalidContract,"Invalid export delivery outcome.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            var current=await Read(db,company,exportId,ct,true) ?? throw new FinancialOperationException(FinancialReasons.InvalidContract,"Export is missing.");
            Require(current.Payload.PortfolioId==portfolioId,FinancialReasons.AuthorityDenied,"Export belongs to another Portfolio.");
            var prior=await db.QueryAsync("""
                SELECT outcome,external_receipt FROM portfolio_financial.accounting_export_attempt
                WHERE destination_company=$1 AND export_id=$2 AND attempt_id=$3;
                """,[company,exportId,attemptId],r=>(Outcome:r.GetString(0),Receipt:r.IsDBNull(1)?null:r.GetString(1)),ct);
            if(prior.Count!=0)
            {
                Require(prior[0].Outcome==outcome && prior[0].Receipt==externalReceipt,FinancialReasons.SourceConflict,"Delivery attempt identity conflicts.");
                return current;
            }
            Require(current.DeliveryStatus!="Delivered" || outcome=="Delivered" && current.ExternalReceipt==externalReceipt,
                FinancialReasons.SourceConflict,"A delivered export cannot be reverted or assigned another receipt.");
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.accounting_export_attempt(destination_company,export_id,attempt_id,outcome,external_receipt)
                VALUES($1,$2,$3,$4,$5);
                """,[company,exportId,attemptId,outcome,externalReceipt],ct);
            var next=current with { Attempts=checked(current.Attempts+1),DeliveryStatus=outcome=="Delivered" ? "Delivered" : "Pending",ExternalReceipt=externalReceipt };
            await db.ExecuteAsync("""
                UPDATE portfolio_financial.accounting_export SET retry_count=$3,delivery_status=$4,external_receipt=$5
                WHERE destination_company=$1 AND export_id=$2;
                """,[company,exportId,next.Attempts,next.DeliveryStatus,next.ExternalReceipt],ct);
            return next;
        },token);
    }

    static async Task<AccountingExportReceipt?> Read(EnlistedEventTransaction db,string company,Guid id,CancellationToken token,bool locked=false)
    {
        var rows=await db.QueryAsync("SELECT metadata::text,delivery_status,retry_count,external_receipt,payload_hash FROM portfolio_financial.accounting_export WHERE destination_company=$1 AND export_id=$2"+(locked?" FOR UPDATE;":";"),
            [company,id],r=>(Receipt:Decode<AccountingExportReceipt>(r.GetString(0)),Status:r.GetString(1),Attempts:r.GetInt32(2),External:r.IsDBNull(3)?null:r.GetString(3),Hash:r.GetString(4)),token);
        if(rows.Count==0) return null;
        var row=rows[0];
        Require(row.Receipt.PayloadHash==row.Hash && FinancialCanonicalHash.Compute(row.Receipt.Payload)==row.Hash,
            FinancialReasons.SourceConflict,"Stored export payload hash mismatch.");
        return row.Receipt with { DeliveryStatus=row.Status,Attempts=row.Attempts,ExternalReceipt=row.External };
    }
    static void Authorize(FinancialAccess access,int portfolioId)
        =>Require(portfolioId>0 && !string.IsNullOrWhiteSpace(access.Principal)
            && (access.Roles.Contains("PortfolioAdministrator") || access.Roles.Contains("LedgerExport") && access.PortfolioIds?.Contains(portfolioId)==true),
            FinancialReasons.AuthorityDenied,"Accounting export permission is required for this Portfolio.");
}
