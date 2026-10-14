using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

// Explicit Development migration, with no schema creation, table deletion or service startup.
if(args.Length!=1 || args[0] is not("--development-writers-stopped" or "--verify-existing-baseline" or "--upgrade-composition-baseline"))
    throw new ArgumentException("Usage: --development-writers-stopped after stopping all IFM application writers.");
var connection=Environment.GetEnvironmentVariable("IFM_FINANCIAL_BASELINE_CONNECTION")
    ?? throw new InvalidOperationException("Set IFM_FINANCIAL_BASELINE_CONNECTION to the Development event-source database.");
var identity=new Npgsql.NpgsqlConnectionStringBuilder(connection);
if(identity.Host is not("localhost" or "127.0.0.1") || identity.Database!="event-source-dev-db")
    throw new InvalidOperationException("This owner-approved migration is limited to the local Development event-source database.");
var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,connection,"System.Data.Postgres");
var transactions=new PostgresEventTransaction(settings);
// Add the new reconstruction checkpoint table without altering existing financial rows.
await transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync("""
    CREATE TABLE IF NOT EXISTS portfolio_financial.financial_projection_checkpoint(
      source_event_id uuid PRIMARY KEY, portfolio_id int NOT NULL,
      event_version bigint NOT NULL UNIQUE REFERENCES event_log(eventVersion), financial_revision bigint NOT NULL);
    """,[],ct));
var reader=new FinancialEventLogReader(transactions);
var cut=Convert.ToInt64(await transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT COALESCE(max(eventversion),0) FROM event_log;",[],ct)));
var existing=await reader.ReadLatestBaselineAsync(cut);
if(args[0]=="--verify-existing-baseline")
{
    if(existing==null) throw new InvalidDataException("No Development baseline exists.");
    await VerifyIsolatedAsync(existing,transactions,identity);
    return;
}
if(existing!=null && (args[0]!="--upgrade-composition-baseline" || existing.SchemaVersion==2)) throw new InvalidOperationException($"Development already has baseline {existing.Id}; inspect it before creating another.");
var source=await new FinancialProjectionBaseline(transactions).CaptureAsync(true,includeComposition:args[0]=="--upgrade-composition-baseline");
var stored=await reader.ReadLatestBaselineAsync(source.EventId) ?? throw new InvalidDataException("Committed baseline was not readable from source bytes.");
if(stored.Id!=source.Id || stored.SourceCut!=source.SourceCut || !stored.Tables.Select(t=>t.Sha256).SequenceEqual(source.Tables.Select(t=>t.Sha256)))
    throw new InvalidDataException("Persisted baseline differs from captured facts.");
Console.WriteLine(JsonSerializer.Serialize(new { database=identity.Database,baselineId=stored.Id,eventVersion=stored.EventId,
    sourceCut=stored.SourceCut,schemaVersion=stored.FinancialSchemaVersion,verifiedAtUtc=DateTime.UtcNow,
    tables=stored.Tables.Select(t=>new { name=t.Name,rowCount=JsonDocument.Parse(t.RowsJson).RootElement.GetArrayLength(),sha256=t.Sha256 }) },new JsonSerializerOptions{WriteIndented=true}));


static async Task VerifyIsolatedAsync(TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialProjectionBaselineEvent source,
    IPostgresEventTransaction original,NpgsqlConnectionStringBuilder identity)
{
    var database="ifm_baseline_verify_"+Guid.NewGuid().ToString("N");
    var adminIdentity=new NpgsqlConnectionStringBuilder(identity.ConnectionString){Database="postgres"};
    await using var admin=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(adminIdentity.ConnectionString);
    await admin.OpenAsync();
    await using(var create=new NpgsqlCommand($"CREATE DATABASE {database}",admin)) await create.ExecuteNonQueryAsync();
    try
    {
        var targetIdentity=new NpgsqlConnectionStringBuilder(identity.ConnectionString){Database=database};
        var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,targetIdentity.ConnectionString,"System.Data.Postgres");
        await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var target=new PostgresEventTransaction(settings);await new PortfolioFinancialSchema(target).InitializeAsync();
        if(source.SchemaVersion==2) await target.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TomasAI.IFM.Application.Storage.PortfolioDb.Schema.PortfolioSchemaSql.Portfolio,[],ct));
        var versions=new HashSet<long>{source.EventId};
        foreach(var image in source.Tables.Where(t=>t.Name is "financial_operation_receipt" or "financial_history_receipt"))
            foreach(var row in JsonDocument.Parse(image.RowsJson).RootElement.EnumerateArray()) versions.Add(row.GetProperty("event_version").GetInt64());
        foreach(var image in source.Tables.Where(t=>t.Name=="portfolio.financial_operation_by_portfolio_month"))
            foreach(var row in JsonDocument.Parse(image.RowsJson).RootElement.EnumerateArray()) versions.Add(row.GetProperty("source_event_id").GetInt64());
        await target.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync("DELETE FROM event_name_id WHERE NOT EXISTS(SELECT 1 FROM event_log);",[],ct));
        foreach(var table in new[]{"event_stream_id","event_name_id","event_log"})
        {
            var filter=table switch
            {
                "event_stream_id"=>"EXISTS(SELECT 1 FROM event_log e WHERE e.eventstreamid=t.eventstreamid AND e.eventversion=ANY($1))",
                "event_name_id"=>"EXISTS(SELECT 1 FROM event_log e WHERE e.eventnameid=t.eventnameid AND e.eventversion=ANY($1))",
                _=>"t.eventversion=ANY($1)"
            };
            var json=await original.ExecuteAsync((db,ct)=>db.ScalarAsync($"SELECT COALESCE(jsonb_agg(to_jsonb(t)),'[]'::jsonb)::text FROM {table} t WHERE {filter};",[versions.ToArray()],ct));
            await target.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync($"INSERT INTO {table} SELECT * FROM jsonb_populate_recordset(NULL::{table},$1::jsonb);",[json],ct));
        }
        var materializer=new FinancialProjectionBaseline(target);await materializer.ReplayAsync(source);await materializer.ReplayAsync(source);
        foreach(var image in source.Tables)
        {
            var json=await target.ExecuteAsync((db,ct)=>db.ScalarAsync($"SELECT COALESCE(jsonb_agg(j ORDER BY j::text),'[]'::jsonb)::text FROM (SELECT to_jsonb(t)-'created_transaction_id' j FROM {(image.Name.Contains('.')?image.Name:"portfolio_financial."+image.Name)} t) q;",[],ct));
            if((string?)json!=image.RowsJson) throw new InvalidDataException($"Baseline mismatch: {image.Name}.");
        }
        Console.WriteLine(JsonSerializer.Serialize(new {baselineId=source.Id,eventVersion=source.EventId,verifiedAtUtc=DateTime.UtcNow,
            isolatedDatabase=database,tableCount=source.Tables.Length,repeatedApplication=true,allBusinessRowsMatched=true,
            originalFinancialRowsChanged=false},new JsonSerializerOptions{WriteIndented=true}));
    }
    finally
    {
        NpgsqlConnection.ClearAllPools();
        await using var drop=new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
    }
}
