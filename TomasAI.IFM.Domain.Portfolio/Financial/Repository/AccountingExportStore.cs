
using System.Globalization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Internal durable export outbox. Remote delivery changes only its checkpoint, never journals, balances or capacity.</summary>
public sealed class AccountingExportStore(IPostgresEventTransaction transactions)
{
    public Task<AccountingExportReceipt> PrepareAsync(AccountingExportRequest request, FinancialAccess access,
        Func<AccountingExportRequest, IReadOnlyList<AccountingJournalSource>, AccountingExportPayload> build, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(request);
        Authorize(access, request.PortfolioId);
        Require(request.ExportId != Guid.Empty && request.BookId > 0 && request.SourceRevision > 0 && request.JournalIds.Length is >= 1 and <= 100
            && request.JournalIds.Distinct().Count() == request.JournalIds.Length && request.JournalIds.All(x => x > 0)
            && request.Mapping is not null && request.Mapping.Version > 0 && !string.IsNullOrWhiteSpace(request.Mapping.DestinationCompany)
            && request.Mapping.DestinationCompany.Length <= 128 && request.Mapping.Accounts.Length is >= 1 and <= 256,
            FinancialReasons.InvalidContract, "Invalid bounded accounting export request.");
        // Freeze the mutable transport arrays before entering an asynchronous database operation.
        request = request with
        {
            JournalIds = request.JournalIds.Order().ToArray(),
            Mapping = request.Mapping with
            { Accounts = request.Mapping.Accounts.OrderBy(x => x.AccountId).ThenBy(x => x.AccountVersion).ToArray() }
        };
        var hash = FinancialCanonicalHash.Compute(request);
        return transactions.ExecuteAsync(async (db, ct) =>
        {
            var authority = await (LockAuthorityAsync(db, request.PortfolioId, null, ct)).ConfigureAwait(false);
            var prior = await (Read(db, request.Mapping.DestinationCompany, request.ExportId, ct)).ConfigureAwait(false);
            if (prior is not null)
            {
                Require(string.Equals(prior.RequestHash,hash,StringComparison.Ordinal) && prior.Payload.PortfolioId == request.PortfolioId,
                    FinancialReasons.SourceConflict, "Export identity was already used with different inputs.");
                return prior;
            }
            Require(authority.Book.BookId == request.BookId && authority.Revision >= request.SourceRevision,
                FinancialReasons.RevisionConflict, "Export is outside this book or its committed source cut.");
            var sources = new List<AccountingJournalSource>();
            foreach (var journalId in request.JournalIds)
            {
                var rows = await (db.QueryAsync(PortfolioDbSql.Financial.AccountingExportStore.Select01, [request.PortfolioId, request.BookId, journalId, request.SourceRevision], r => new AccountingJournalSource(
                        new(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetInt32(3), r.GetFieldValue<DateOnly>(4), r.GetString(5), []),
                        r.GetInt64(6), r.IsDBNull(7) ? null : r.GetInt64(7)), ct)).ConfigureAwait(false);
                Require(rows.Count == 1, FinancialReasons.InvalidContract, "Export journal is missing or outside the exact source cut.");
                var lines = await (db.QueryAsync(PortfolioDbSql.Financial.AccountingExportStore.Select02, [journalId], r => new FinancialJournalEntry(r.GetInt32(0), r.GetInt32(1), r.GetInt64(2), r.IsDBNull(3) ? null : r.GetInt32(3),
                        r.GetDecimal(4), r.GetDecimal(5), r.GetString(6)), ct)).ConfigureAwait(false);
                Require(lines.Count is >= 2 and <= 256, FinancialReasons.InvalidContract, "Export journal line count is invalid.");
                sources.Add(rows[0] with { Journal = rows[0].Journal with { Entries = lines.ToArray() } });
            }
            var payload = build(request, sources);
            Require(payload.ExportId == request.ExportId && payload.PortfolioId == request.PortfolioId && payload.BookId == request.BookId
                && string.Equals(payload.DestinationCompany,request.Mapping.DestinationCompany,StringComparison.Ordinal) && payload.SourceRevision == request.SourceRevision,
                FinancialReasons.InvalidContract, "Export builder changed source ownership.");
            var receipt = new AccountingExportReceipt(payload, FinancialCanonicalHash.Compute(payload), hash, "Pending", 0, null);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Insert01, [request.Mapping.DestinationCompany,request.ExportId,Json(request.JournalIds),request.SourceRevision.ToString(CultureInfo.InvariantCulture),
                    receipt.PayloadHash,request.Mapping.Version,Json(receipt)], ct)).ConfigureAwait(false);
            foreach (var source in request.JournalIds)
                await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Insert02, [request.Mapping.DestinationCompany, source, request.ExportId], ct)).ConfigureAwait(false);
            var sourceEvent=new AccountingExportSnapshotEvent { Id=Guid.NewGuid(),CommandId=request.ExportId,
                AggregateId=request.ExportId.ToString("N"),ReceivedOn=DateTime.UtcNow,PortfolioId=request.PortfolioId,PreparedRequest=request,Receipt=receipt };
            await (FinancialOperationalEventPersistence.AppendAsync(db,Stream(request.Mapping.DestinationCompany,request.ExportId),sourceEvent,request.PortfolioId,ct)).ConfigureAwait(false);
            return receipt;
        }, token);
    }

    public Task<AccountingExportReceipt?> ReadAsync(int portfolioId, string company, Guid exportId, FinancialAccess access, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        Authorize(access, portfolioId);
        return transactions.ExecuteAsync(async (db, ct) =>
        {
            var receipt = await (Read(db, company, exportId, ct)).ConfigureAwait(false);
            Require(receipt is null || receipt.Payload.PortfolioId == portfolioId, FinancialReasons.AuthorityDenied, "Export belongs to another Portfolio.");
            return receipt;
        }, token);
    }

    /// <summary>Records one stable delivery attempt. Unknown or failed delivery keeps the original payload pending for reconciliation.</summary>
    public Task<AccountingExportReceipt> RecordAttemptAsync(int portfolioId, string company, Guid exportId, Guid attemptId,
        string outcome, string? externalReceipt, FinancialAccess access, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        Authorize(access, portfolioId);
        Require(attemptId != Guid.Empty && outcome is "Delivered" or "Failed" or "Unknown"
            && (string.Equals(outcome,"Delivered",StringComparison.Ordinal) ? !string.IsNullOrWhiteSpace(externalReceipt) && externalReceipt.Length <= 256 : externalReceipt is null),
            FinancialReasons.InvalidContract, "Invalid export delivery outcome.");
        return transactions.ExecuteAsync(async (db, ct) =>
        {
            var current = await (Read(db, company, exportId, ct, true)).ConfigureAwait(false) ?? throw new FinancialOperationException(FinancialReasons.InvalidContract, "Export is missing.");
            Require(current.Payload.PortfolioId == portfolioId, FinancialReasons.AuthorityDenied, "Export belongs to another Portfolio.");
            var prior = await (db.QueryAsync(PortfolioDbSql.Financial.AccountingExportStore.Select03, [company, exportId, attemptId], r => (Outcome: r.GetString(0), Receipt: r.IsDBNull(1) ? null : r.GetString(1)), ct)).ConfigureAwait(false);
            if (prior.Count != 0)
            {
                Require(string.Equals(prior[0].Outcome,outcome,StringComparison.Ordinal) && string.Equals(prior[0].Receipt,externalReceipt,StringComparison.Ordinal), FinancialReasons.SourceConflict, "Delivery attempt identity conflicts.");
                return current;
            }
            Require(!string.Equals(current.DeliveryStatus,"Delivered",StringComparison.Ordinal) || string.Equals(outcome,"Delivered",StringComparison.Ordinal) && string.Equals(current.ExternalReceipt,externalReceipt,StringComparison.Ordinal),
                FinancialReasons.SourceConflict, "A delivered export cannot be reverted or assigned another receipt.");
            await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Insert03, [company, exportId, attemptId, outcome, externalReceipt], ct)).ConfigureAwait(false);
            var next = current with { Attempts = checked(current.Attempts + 1), DeliveryStatus = string.Equals(outcome,"Delivered",StringComparison.Ordinal) ? "Delivered" : "Pending", ExternalReceipt = externalReceipt };
            await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Update01, [company, exportId, next.Attempts, next.DeliveryStatus, next.ExternalReceipt], ct)).ConfigureAwait(false);
            var sourceEvent=new AccountingExportSnapshotEvent { Id=Guid.NewGuid(),CommandId=attemptId,AggregateId=exportId.ToString("N"),
                ReceivedOn=DateTime.UtcNow,PortfolioId=portfolioId,Receipt=next,AttemptId=attemptId,Outcome=outcome };
            await (FinancialOperationalEventPersistence.AppendAsync(db,Stream(company,exportId),sourceEvent,portfolioId,ct)).ConfigureAwait(false);
            return next;
        }, token);
    }

    /// <summary>Restores export payload and accepted delivery outcomes without remote delivery.</summary>
    public Task ReplayAsync(AccountingExportSnapshotEvent source,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.SchemaVersion!=1 || source.EventId<=0 || source.Id==Guid.Empty) throw new InvalidDataException("Committed export facts required.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01,[source.PortfolioId],ct)).ConfigureAwait(false);
            if(!await (FinancialOperationalEventPersistence.ClaimAsync(db,source,source.PortfolioId,ct)).ConfigureAwait(false)) return false;
            var receipt=source.Receipt;var payload=receipt.Payload;
            if(source.PreparedRequest is {} request)
            {
                await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Insert01,
                    [payload.DestinationCompany,payload.ExportId,Json(request.JournalIds),request.SourceRevision.ToString(CultureInfo.InvariantCulture),receipt.PayloadHash,request.Mapping.Version,Json(receipt)],ct)).ConfigureAwait(false);
                foreach(var journal in request.JournalIds) await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Insert02,[payload.DestinationCompany,journal,payload.ExportId],ct)).ConfigureAwait(false);
            }
            else
            {
                if(source.AttemptId is null) throw new InvalidDataException("Export attempt facts are missing.");
                await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Insert03,[payload.DestinationCompany,payload.ExportId,source.AttemptId,source.Outcome,receipt.ExternalReceipt],ct)).ConfigureAwait(false);
                Require(await (db.ExecuteAsync(PortfolioDbSql.Financial.AccountingExportStore.Update01,[payload.DestinationCompany,payload.ExportId,receipt.Attempts,receipt.DeliveryStatus,receipt.ExternalReceipt],ct)).ConfigureAwait(false)==1,
                    FinancialReasons.RevisionConflict,"Required preceding export fact is missing.");
            }
            return true;
        },token);
    }

    static string Stream(string company,Guid exportId)=>$"AccountingExportSnapshot.{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(company)))}.{exportId:N}";

    static async Task<AccountingExportReceipt?> Read(EnlistedEventTransaction db, string company, Guid id, CancellationToken token, bool locked = false)
    {
        var rows = await (db.QueryAsync(PortfolioDbSql.Financial.AccountingExportStore.Select04 + (locked ? " FOR UPDATE;" : ";"),
            [company, id], r => (Receipt: Decode<AccountingExportReceipt>(r.GetString(0)), Status: r.GetString(1), Attempts: r.GetInt32(2), External: r.IsDBNull(3) ? null : r.GetString(3), Hash: r.GetString(4)), token)).ConfigureAwait(false);
        if (rows.Count == 0) return null;
        var row = rows[0];
        Require(string.Equals(row.Receipt.PayloadHash,row.Hash,StringComparison.Ordinal) && string.Equals(FinancialCanonicalHash.Compute(row.Receipt.Payload),row.Hash,StringComparison.Ordinal),
            FinancialReasons.SourceConflict, "Stored export payload hash mismatch.");
        return row.Receipt with { DeliveryStatus = row.Status, Attempts = row.Attempts, ExternalReceipt = row.External };
    }
    static void Authorize(FinancialAccess access, int portfolioId)
        => Require(portfolioId > 0 && !string.IsNullOrWhiteSpace(access.Principal)
            && (access.Roles.Contains("PortfolioAdministrator") || access.Roles.Contains("LedgerExport") && access.PortfolioIds?.Contains(portfolioId) == true),
            FinancialReasons.AuthorityDenied, "Accounting export permission is required for this Portfolio.");
}
