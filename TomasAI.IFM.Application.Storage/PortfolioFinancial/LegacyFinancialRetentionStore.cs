using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public sealed record LegacyFinancialRetentionManifest(LegacyFinancialInventoryScope Scope,LegacyFinancialInventoryResult Inventory,
    long MappedFundRevision,long PortfolioRevision,decimal CapitalRecognized,string Principal,string Reason);

/// <summary>Seals retained source evidence without creating a book, journal, balance or spending permission.</summary>
public sealed class LegacyFinancialRetentionStore(IPostgresEventTransaction transactions)
{
    public Task<LegacyFinancialInventoryResult?> ReadAsync(LegacyFinancialInventoryScope scope,string principal,string reason,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,ct)=>
    {
        var json=await db.ScalarAsync("SELECT verified_totals::text FROM portfolio_financial.ledger_migration WHERE migration_id=$1 AND cutover_state='RetainedReadOnly';",[scope.InventoryId],ct) as string;
        if(json is null) return null;
        var manifest=Decode<LegacyFinancialRetentionManifest>(json);
        Require(manifest.Scope==scope && manifest.Principal==principal && manifest.Reason==reason,FinancialReasons.RequestMismatch,"Retention identity belongs to another request.");
        return manifest.Inventory with { State="RetainedReadOnly" };
    },token);
    public Task SealAsync(LegacyFinancialInventoryScope scope,LegacyFinancialInventoryResult inventory,long mappedFundRevision,long portfolioRevision,
        string principal,string reason,CancellationToken token=default)=>transactions.ExecuteAsync(async(db,ct)=>
    {
        Require(scope.ImportMode=="ReadOnlyHistoryWithDevelopmentCapital" && scope.InventoryId==inventory.InventoryId &&
            !string.IsNullOrWhiteSpace(principal) && !string.IsNullOrWhiteSpace(reason) && reason.Length<=1024,
            FinancialReasons.InvalidContract,"An explicit retained-history inventory and audit reason are required.");
        await db.ScalarAsync("SELECT pg_advisory_xact_lock(34101,$1);",[scope.SourceFundId],ct);
        var saved=(await db.QueryAsync("SELECT scope_hash,result::text FROM portfolio_financial.legacy_financial_inventory WHERE inventory_id=$1 AND state='UnfencedInventory' FOR SHARE;",
            [scope.InventoryId],r=>(Scope:r.GetString(0),Result:r.GetString(1)),ct)).SingleOrDefault();
        Require(saved.Scope==FinancialCanonicalHash.Compute(scope) && saved.Result is not null && Decode<LegacyFinancialInventoryResult>(saved.Result)==inventory,
            FinancialReasons.RequestMismatch,"Retention must match the completed immutable inventory.");
        Require(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.legacy_write_intent WHERE fund_id=$1 AND state='Pending';",[scope.SourceFundId],ct) is 0L,
            FinancialReasons.AuthorityDenied,"Uncertain legacy writes still require recovery.");
        Require(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.legacy_writer_scope WHERE fund_id=$1 AND portfolio_id=$2 AND qualification_id=$3 AND state='Fenced';",
            [scope.SourceFundId,scope.DestinationPortfolioId,scope.InventoryId],ct) is 1L,FinancialReasons.AuthorityDenied,"Retained source writer fence is missing.");
        var version=await db.ScalarAsync("SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;",[$"PortfolioFund.{scope.DestinationPortfolioId}.{scope.DestinationFundId}"],ct);
        Require(mappedFundRevision>0 && version is not null && Convert.ToInt64(version)==mappedFundRevision,FinancialReasons.AuthorityRevoked,"Historical Fund mapping changed during retention.");
        var parentVersion=await db.ScalarAsync("SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;",[$"Portfolio.{scope.DestinationPortfolioId}"],ct);
        Require(portfolioRevision>0 && parentVersion is not null && Convert.ToInt64(parentVersion)==portfolioRevision,FinancialReasons.AuthorityRevoked,"Historical Portfolio membership changed during retention.");
        var manifest=new LegacyFinancialRetentionManifest(scope,inventory,mappedFundRevision,portfolioRevision,0m,principal,reason);
        var hash=FinancialCanonicalHash.Compute(manifest);
        var existing=await db.ScalarAsync("SELECT manifest_hash FROM portfolio_financial.ledger_migration WHERE migration_id=$1;",[scope.InventoryId],ct);
        if(existing is not null) { Require(Equals(existing,hash),FinancialReasons.RequestMismatch,"Retention identity belongs to different evidence.");return true; }
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.ledger_migration(migration_id,portfolio_id,mappings,mode,source_watermark,destination_watermark,
              manifest_hash,verified_totals,writer_fence,cutover_state)
            VALUES($1,$2,$3,'ReadOnlyHistoryWithDevelopmentCapital',$4,'NoFinancialPosting',$5,$6,$7,'RetainedReadOnly');
            """,[scope.InventoryId,scope.DestinationPortfolioId,Json(scope),inventory.ContentHash,hash,Json(manifest),scope.InventoryId.ToString("N")],ct);
        return true;
    },token);
}
