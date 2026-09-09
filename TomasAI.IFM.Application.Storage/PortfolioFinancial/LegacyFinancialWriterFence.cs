using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>
/// Persists legacy write intent before Scylla I/O. A failed or lost reply retains the intent,
/// so cutover cannot mistake an uncertain cross-database write for a drained writer.
/// Financial transactions never hold PostgreSQL locks while waiting for Scylla.
/// </summary>
public sealed class LegacyFinancialWriterFence(IPostgresEventTransaction transactions)
{
    public Task<Guid> BeginWriteAsync(IEnumerable<int> fundIds,CancellationToken token=default)
    {
        var funds=fundIds.Distinct().Order().ToArray();
        if(funds.Length is 0 or >256 || funds.Any(x=>x<=0)) throw new ArgumentException("A bounded legacy Fund scope is required.");
        var ticket=Guid.NewGuid();
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            foreach(var fund in funds)
            {
                await LockScope(db,fund,ct);
                Require(await db.ScalarAsync("SELECT state FROM portfolio_financial.legacy_writer_scope WHERE fund_id=$1;",[fund],ct) is "Legacy",
                    FinancialReasons.AuthorityDenied,"Legacy transaction writes are fenced for this Fund.");
                await db.ExecuteAsync("INSERT INTO portfolio_financial.legacy_write_intent(ticket_id,fund_id,state) VALUES($1,$2,'Pending');",[ticket,fund],ct);
            }
            return ticket;
        },token);
    }

    /// <summary>Only confirmed completion of all Scylla writes/projections retires the durable intent.</summary>
    public Task CompleteWriteAsync(Guid ticket,CancellationToken token=default)=>transactions.ExecuteAsync(async(db,ct)=>
    {
        await db.ExecuteAsync("UPDATE portfolio_financial.legacy_write_intent SET state='Completed' WHERE ticket_id=$1;",[ticket],ct);
        return true;
    },token);

    /// <summary>
    /// Reserves exclusive ownership only for a fresh scope with no prior legacy event stream or write intent.
    /// Existing legacy scopes require the separate reconciled migration procedure; this cannot bypass it.
    /// </summary>
    public Task FreezeFreshScopeAsync(int portfolioId,int fundId,Guid qualificationId,CancellationToken token=default)
    {
        if(portfolioId<=0 || fundId<=0 || qualificationId==Guid.Empty) throw new ArgumentException("Exact qualification scope is required.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            await LockScope(db,fundId,ct);
            var state=(await db.QueryAsync("SELECT state,portfolio_id,qualification_id FROM portfolio_financial.legacy_writer_scope WHERE fund_id=$1;",[fundId],
                r=>(State:r.GetString(0),Portfolio:r.IsDBNull(1)?0:r.GetInt32(1),Id:r.IsDBNull(2)?Guid.Empty:r.GetGuid(2)),ct)).Single();
            if(state.State=="Fenced")
            {
                Require(state.Portfolio==portfolioId && state.Id==qualificationId,FinancialReasons.RequestMismatch,"Fund writer scope belongs to another qualification.");return true;
            }
            Require(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.legacy_write_intent WHERE fund_id=$1;",[fundId],ct) is 0L,
                FinancialReasons.AuthorityDenied,"Legacy write history or an uncertain write requires reconciliation before cutover.");
            var prefix=$"Command.FundTransactionCommand.{fundId}.%";
            var fundPrefix=$"Command.FundCommand.{fundId}";
            Require(await db.ScalarAsync("SELECT count(*) FROM event_stream_id WHERE eventstream LIKE $1 OR eventstream=$2 OR eventstream LIKE $3;",[prefix,fundPrefix,fundPrefix+".%"],ct) is 0L,
                FinancialReasons.AuthorityDenied,"Existing legacy event history requires a reconciled migration.");
            await db.ExecuteAsync("UPDATE portfolio_financial.legacy_writer_scope SET state='Fenced',portfolio_id=$2,qualification_id=$3 WHERE fund_id=$1;",[fundId,portfolioId,qualificationId],ct);
            return true;
        },token);
    }

    static async Task LockScope(EnlistedEventTransaction db,int fundId,CancellationToken ct)
    {
        await db.ScalarAsync("SELECT pg_advisory_xact_lock(34101,$1);",[fundId],ct);
        await db.ExecuteAsync("INSERT INTO portfolio_financial.legacy_writer_scope(fund_id,state) VALUES($1,'Legacy') ON CONFLICT DO NOTHING;",[fundId],ct);
    }

    /// <summary>Stops new legacy writes while retaining completed source history. Pending intents remain visible and block retention completion.</summary>
    public Task<long> FreezeRetainedScopeAsync(int portfolioId,int fundId,Guid retentionId,CancellationToken token=default)
    {
        if(portfolioId<=0 || fundId<=0 || retentionId==Guid.Empty) throw new ArgumentException("Exact retained-history scope is required.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            await LockScope(db,fundId,ct);
            var state=(await db.QueryAsync("SELECT state,portfolio_id,qualification_id FROM portfolio_financial.legacy_writer_scope WHERE fund_id=$1;",[fundId],
                r=>(State:r.GetString(0),Portfolio:r.IsDBNull(1)?0:r.GetInt32(1),Id:r.IsDBNull(2)?Guid.Empty:r.GetGuid(2)),ct)).Single();
            if(state.State=="Fenced")
                Require(state.Portfolio==portfolioId && state.Id==retentionId,FinancialReasons.RequestMismatch,"Legacy scope is owned by another retention or qualification.");
            else
                await db.ExecuteAsync("UPDATE portfolio_financial.legacy_writer_scope SET state='Fenced',portfolio_id=$2,qualification_id=$3 WHERE fund_id=$1;",[fundId,portfolioId,retentionId],ct);
            return Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.legacy_write_intent WHERE fund_id=$1 AND state='Pending';",[fundId],ct));
        },token);
    }

    /// <summary>Records a completed empty-source read made after fencing; callers must not use this to qualify existing legacy history.</summary>
    public Task VerifyEmptySourceAsync(int portfolioId,int fundId,Guid qualificationId,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,ct)=>
    {
        await LockScope(db,fundId,ct);
        Require(await db.ExecuteAsync("UPDATE portfolio_financial.legacy_writer_scope SET empty_verified=true WHERE fund_id=$1 AND portfolio_id=$2 AND qualification_id=$3 AND state='Fenced';",
            [fundId,portfolioId,qualificationId],ct)==1,FinancialReasons.AuthorityDenied,"Fresh-source writer fence does not match qualification.");
        return true;
    },token);
}
