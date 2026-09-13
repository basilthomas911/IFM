using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public sealed record LegacyFinancialRetentionManifest(LegacyFinancialInventoryScope Scope,LegacyFinancialInventoryResult Inventory,
    long MappedFundRevision,long PortfolioRevision,decimal CapitalRecognized,string Principal,string Reason);

/// <summary>Seals retained source evidence without creating a book, journal, balance or spending permission.</summary>
public sealed class LegacyFinancialRetentionStore(IPostgresEventTransaction transactions)
{
    public Task<LegacyFinancialInventoryResult?> ReadAsync(LegacyFinancialInventoryScope scope,string principal,string reason,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,ct)=>
    {
        var json=await db.ScalarAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select01,[scope.InventoryId],ct) as string;
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
        await db.ScalarAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select02,[scope.SourceFundId],ct);
        var saved=(await db.QueryAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select03,
            [scope.InventoryId],r=>(Scope:r.GetString(0),Result:r.GetString(1)),ct)).SingleOrDefault();
        Require(saved.Scope==FinancialCanonicalHash.Compute(scope) && saved.Result is not null && Decode<LegacyFinancialInventoryResult>(saved.Result)==inventory,
            FinancialReasons.RequestMismatch,"Retention must match the completed immutable inventory.");
        Require(await db.ScalarAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select04,[scope.SourceFundId],ct) is 0L,
            FinancialReasons.AuthorityDenied,"Uncertain legacy writes still require recovery.");
        Require(await db.ScalarAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select05,
            [scope.SourceFundId,scope.DestinationPortfolioId,scope.InventoryId],ct) is 1L,FinancialReasons.AuthorityDenied,"Retained source writer fence is missing.");
        var version=await db.ScalarAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select06,[$"PortfolioFund.{scope.DestinationPortfolioId}.{scope.DestinationFundId}"],ct);
        Require(mappedFundRevision>0 && version is not null && Convert.ToInt64(version)==mappedFundRevision,FinancialReasons.AuthorityRevoked,"Historical Fund mapping changed during retention.");
        var parentVersion=await db.ScalarAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select07,[$"Portfolio.{scope.DestinationPortfolioId}"],ct);
        Require(portfolioRevision>0 && parentVersion is not null && Convert.ToInt64(parentVersion)==portfolioRevision,FinancialReasons.AuthorityRevoked,"Historical Portfolio membership changed during retention.");
        var manifest=new LegacyFinancialRetentionManifest(scope,inventory,mappedFundRevision,portfolioRevision,0m,principal,reason);
        var hash=FinancialCanonicalHash.Compute(manifest);
        var existing=await db.ScalarAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Select08,[scope.InventoryId],ct);
        if(existing is not null) { Require(Equals(existing,hash),FinancialReasons.RequestMismatch,"Retention identity belongs to different evidence.");return true; }
        await db.ExecuteAsync(PortfolioDbSql.Financial.LegacyFinancialRetentionStore.Insert01,[scope.InventoryId,scope.DestinationPortfolioId,Json(scope),inventory.ContentHash,hash,Json(manifest),scope.InventoryId.ToString("N")],ct);
        return true;
    },token);
}
