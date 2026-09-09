using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public sealed record LegacyFinancialInventoryScope(Guid InventoryId,int SourceFundId,int DestinationPortfolioId,int DestinationFundId,
    DateOnly Start,DateOnly End,string SourceEnvironment,string ImportMode);
public sealed record LegacyFinancialInventoryRow(string SourceKey,string SourceHash,string SourcePayload,string Disposition,string Reason,decimal Amount);
public sealed record LegacyFinancialInventoryResult(Guid InventoryId,long Rows,long HistoricalOnly,long Quarantined,decimal SourceAmount,string ContentHash,string State);

/// <summary>Immutable dry-run source evidence. An unfenced inventory never qualifies capital, switches writers or imports journals.</summary>
public sealed class LegacyFinancialInventoryStore(IPostgresEventTransaction transactions)
{
    public Task BeginAsync(LegacyFinancialInventoryScope scope,CancellationToken token)
    {
        if(scope.InventoryId==Guid.Empty || scope.SourceFundId<=0 || scope.DestinationPortfolioId<=0 || scope.DestinationFundId<=0 ||
            scope.Start==default || scope.End<scope.Start || string.IsNullOrWhiteSpace(scope.SourceEnvironment) ||
            scope.ImportMode is not ("FullPostedHistory" or "OpeningBalanceWithHistory" or "ReadOnlyHistoryWithDevelopmentCapital")) throw new ArgumentException("Complete inventory scope is required.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.legacy_financial_inventory(inventory_id,scope,scope_hash,state)
                VALUES($1,$2,$3,'Incomplete') ON CONFLICT(inventory_id) DO NOTHING;
                """,[scope.InventoryId,Json(scope),FinancialCanonicalHash.Compute(scope)],ct);
            var existing=await db.ScalarAsync("SELECT scope_hash FROM portfolio_financial.legacy_financial_inventory WHERE inventory_id=$1 FOR UPDATE;",[scope.InventoryId],ct);
            Require(Equals(existing,FinancialCanonicalHash.Compute(scope)),FinancialReasons.RequestMismatch,"Inventory identity belongs to a different source scope.");
            return true;
        },token);
    }
    public Task SaveRowAsync(Guid id,LegacyFinancialInventoryRow row,CancellationToken token)
    {
        if(row.SourceKey.Length!=64 || row.SourceHash.Length!=64 || row.SourcePayload.Length>1048576 ||
            row.Disposition is not ("HistoricalOnly" or "Quarantined") || string.IsNullOrWhiteSpace(row.Reason))
            throw new ArgumentException("An inventory row requires bounded, classified source evidence.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            var state=await db.ScalarAsync("SELECT state FROM portfolio_financial.legacy_financial_inventory WHERE inventory_id=$1 FOR UPDATE;",[id],ct);
            Require(state is "Incomplete" or "UnfencedInventory",FinancialReasons.InvalidContract,"Inventory has not been initialized.");
            var saved=await db.ScalarAsync("SELECT row_hash FROM portfolio_financial.legacy_financial_inventory_row WHERE inventory_id=$1 AND source_key=$2;",[id,row.SourceKey],ct);
            var hash=FinancialCanonicalHash.Compute(row);
            if(saved is not null)
            {
                Require(Equals(saved,hash),FinancialReasons.RequestMismatch,"Legacy source changed since this inventory was recorded.");return false;
            }
            Require(state is "Incomplete",FinancialReasons.RequestMismatch,"Completed inventory gained a source row; create a new inventory and reconcile the difference.");
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.legacy_financial_inventory_row(inventory_id,source_key,source_hash,payload,disposition,reason,amount,row_hash)
                VALUES($1,$2,$3,$4::jsonb,$5,$6,$7,$8);
                """,[id,row.SourceKey,row.SourceHash,row.SourcePayload,row.Disposition,row.Reason,row.Amount,hash],ct);
            return true;
        },token);
    }
    public async Task<LegacyFinancialInventoryResult> CompleteAsync(Guid id,long observedRows,CancellationToken token)
    {
        // Hash bounded pages outside transactions. Each recorded row is immutable; final count/state CAS
        // detects a concurrent scan that added rows rather than publishing an incomplete source set.
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);string after="";long count=0;
        while(true)
        {
            var rows=await transactions.ExecuteAsync((db,ct)=>db.QueryAsync("""
                SELECT source_key,row_hash FROM portfolio_financial.legacy_financial_inventory_row
                WHERE inventory_id=$1 AND source_key>$2 ORDER BY source_key LIMIT 128;
                """,[id,after],r=>(Key:r.GetString(0),Hash:r.GetString(1)),ct),token);
            foreach(var row in rows) { hash.AppendData(Encoding.ASCII.GetBytes(row.Key+row.Hash));count++; }
            if(rows.Count<128) break;after=rows[^1].Key;
        }
        var contentHash=Convert.ToHexString(hash.GetHashAndReset());
        return await transactions.ExecuteAsync(async(db,ct)=>
        {
            var state=await db.ScalarAsync("SELECT state FROM portfolio_financial.legacy_financial_inventory WHERE inventory_id=$1 FOR UPDATE;",[id],ct);
            Require(state is "Incomplete" or "UnfencedInventory",FinancialReasons.InvalidContract,"Inventory has not been initialized.");
            var totals=(await db.QueryAsync("""
                SELECT count(*),count(*) FILTER(WHERE disposition='HistoricalOnly'),count(*) FILTER(WHERE disposition='Quarantined'),coalesce(sum(amount),0)
                FROM portfolio_financial.legacy_financial_inventory_row WHERE inventory_id=$1;
                """,[id],r=>(Rows:r.GetInt64(0),History:r.GetInt64(1),Quarantine:r.GetInt64(2),Amount:r.GetDecimal(3)),ct)).Single();
            Require(count==observedRows && totals.Rows==count,FinancialReasons.RequestMismatch,"Source row count changed; this inventory is not a stable source cut.");
            var result=new LegacyFinancialInventoryResult(id,count,totals.History,totals.Quarantine,totals.Amount,contentHash,"UnfencedInventory");
            await db.ExecuteAsync("UPDATE portfolio_financial.legacy_financial_inventory SET state='UnfencedInventory',result=$2 WHERE inventory_id=$1;",[id,Json(result)],ct);
            return result;
        },token);
    }
}
