
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;
namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Rebuilds accepted composition facts and their authority revision, without composing or submitting orders.</summary>
public sealed class PortfolioOrderCompositionProjection(IPostgresEventTransaction transactions)
{
    public Task ReplayAsync(IFinancialCompletedEvent source,CancellationToken token=default)
    {
        var revision=source switch
        {
            PortfolioOrderCompositionCompletedEvent value=>value.Receipt.FinancialRevision,
            PortfolioCloseOrderCompositionCompletedEvent { SchemaVersion:2,ProjectionFacts:not null } value=>value.Receipt.FinancialRevision,
            _=>throw new InvalidDataException("Complete accepted composition source required; old close sources need a verified baseline.")
        };
        if(source.EventId<=0 || source.Id==Guid.Empty || revision<=0) throw new InvalidDataException("Committed composition source required.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01,[source.PortfolioId],ct)).ConfigureAwait(false);
            if(!await (LedgerFinancialProjection.RecordAppliedAsync(db,source,revision,ct)).ConfigureAwait(false)) return false;
            var authority=await (LockAuthorityAsync(db,source.PortfolioId,null,ct)).ConfigureAwait(false);
            if(authority.Revision+1!=revision) throw new InvalidDataException("Composition sources must follow financial revision order.");
            if(source is PortfolioOrderCompositionCompletedEvent opening) await (PortfolioOrderCompositionStore.ProjectMaterialAsync(db,opening,ct)).ConfigureAwait(false);
            else await (PortfolioCloseOrderCompositionStore.ProjectMaterialAsync(db,(PortfolioCloseOrderCompositionCompletedEvent)source,ct)).ConfigureAwait(false);
            var actor=source is PortfolioCloseOrderCompositionCompletedEvent closing?closing.ProjectionFacts!.ActorName:source.Subject.Name;
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert06,
                [source.PortfolioId,source.OperationId,actor,source.InputHash,source.EventId,revision],ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01,[source.PortfolioId,revision],ct)).ConfigureAwait(false);
            return true;
        },token);
    }
}
