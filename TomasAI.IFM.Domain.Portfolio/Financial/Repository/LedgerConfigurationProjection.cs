
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;
namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Restores accepted configuration facts without admission checks or recalculating reconciliation.</summary>
public sealed class LedgerConfigurationProjection(IPostgresEventTransaction transactions)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task ReplayAsync(LedgerConfigurationCompletedEvent source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var facts = source.ProjectionFacts;
        if (facts is not { Version: 1 } || source.EventId <= 0 || source.Id == Guid.Empty)
            throw new InvalidDataException("Configuration reconstruction requires a committed event containing accepted facts.");
        return transactions.ExecuteAsync(async (db, ct) =>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01, [source.PortfolioId], ct)).ConfigureAwait(false);
            if (!await (LedgerFinancialProjection.RecordAppliedAsync(db, source, source.Receipt.FinancialRevision, ct)).ConfigureAwait(false)) return false;
            var body = facts.Request;
            if (body.Action == LedgerConfigurationAction.CreateBook)
                await (CreateBookAsync(db, facts.AcceptedBook, body.Accounts, body.Rules, body.PeriodStart, body.PeriodEnd, body.PeriodId, ct)).ConfigureAwait(false);
            else
            {
                var current = await (LockAuthorityAsync(db, source.PortfolioId, null, ct)).ConfigureAwait(false);
                if (source.Receipt.FinancialRevision != current.Revision + 1)
                    throw new InvalidDataException("Financial configuration source events must be rebuilt in revision order.");
            }
            switch (body.Action)
            {
                case LedgerConfigurationAction.AddAccountVersion:
                    foreach (var a in body.Accounts)
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert02,
                            [body.BookId,a.AccountId,a.Version,a.Category,(int)a.NormalSide,a.FundDimensionRequired,a.ContentHash],ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.AddPostingRuleVersion:
                    foreach (var r in body.Rules)
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert03,
                            [body.BookId,r.RuleId,r.Version,(int)r.Kind,Json(r),r.ContentHash,body.PeriodStart],ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.RetireAccount:
                    foreach (var a in body.Accounts)
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update02,[body.BookId,a.AccountId,a.Version],ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.RetirePostingRule:
                    foreach (var r in body.Rules)
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update03,[body.BookId,r.RuleId,r.Version],ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.OpenPeriod:
                    await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert04,
                        [body.BookId,body.PeriodId,body.PeriodStart,body.PeriodEnd,body.SourceCut,Json(new { source.OperationId,body.Reason })],ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.ClosePeriod:
                case LedgerConfigurationAction.ReopenPeriod:
                    var closing=body.Action==LedgerConfigurationAction.ClosePeriod;
                    Require(await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update04,
                        [body.BookId,body.PeriodId,body.ExpectedVersion,closing?"Closed":"Open",body.SourceCut,
                         Json(new {source.OperationId,body.Reason,body.ReconciliationId}),closing?"Open":"Closed"],ct)).ConfigureAwait(false)==1,
                        FinancialReasons.RevisionConflict,"Required preceding period fact is missing.");
                    break;
                case LedgerConfigurationAction.Reconcile:
                    var result=facts.Reconciliation ?? throw new InvalidDataException("Accepted reconciliation facts are missing.");
                    await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert05,
                        [result.ReconciliationId,body.BookId,source.PortfolioId,body.SourceCut,
                         Json(new {result.JournalCount,result.EntryCount,result.FinancialRevision}),Json(new {result.Debits,result.Credits}),
                         result.ContentHash,Json(result.Differences),result.Differences.Length==0 && result.Debits==result.Credits?"Matched":"Mismatch"],ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.CreateBook:
                case LedgerConfigurationAction.QualifyDevelopmentBook:
                case LedgerConfigurationAction.RefreshAuthority: break;
                default: throw new InvalidDataException("Unsupported accepted configuration action.");
            }
            var book=facts.AcceptedBook;
            await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationProjection.SetAuthority,
                [source.PortfolioId,Json(book),facts.MigrationState,source.Receipt.OperatingState,book.SourceWatermark,book.ValuationWatermark,book.AuthorityEpoch],ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert06,
                [source.PortfolioId,source.OperationId,facts.ActorName,source.InputHash,source.EventId,source.Receipt.FinancialRevision],ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01,[source.PortfolioId,source.Receipt.FinancialRevision],ct)).ConfigureAwait(false);
            return true;
        },token);
    }
}
