using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class ParameterVersionPagingBenchmarks
{
    const int Sets = 24;
    const int Versions = 12_000;
    const int PageSize = 32;
    const string Component = "fixture";
    const string Legacy = "GetParameterSets";
    const string Catalog = "VersionPage";

    [Params(Legacy, Catalog)] public string Query { get; set; } = null!;
    [Params(false, true)] public bool Deep { get; set; }

    NpgsqlConnection db = null!;
    NpgsqlCommand current = null!;
    NpgsqlCommand bounded = null!;

    const string LegacyBaseline =
        """
        SELECT v.body::text, s.name, s.description, s.revision
        FROM reference_configuration.parameter_set_version v
        JOIN reference_configuration.parameter_set s USING (set_id)
        WHERE s.component_code = $1
          AND ($2::uuid IS NULL OR s.set_id = $2)
          AND ($4::uuid IS NULL OR (s.name, s.set_id, -v.version) > ($3, $4, -$5::integer))
        ORDER BY s.name, s.set_id, v.version DESC
        LIMIT $6;
        """;
    const string CatalogBaseline =
        """
        SELECT m.owner_set_id, m.owner_version, s.name, s.description, s.revision
        FROM reference_configuration.parameter_catalog_version m
        JOIN reference_configuration.parameter_set s ON s.set_id = m.owner_set_id
        WHERE s.component_code = $1
          AND ($2::uuid IS NULL OR s.set_id = $2)
          AND ($4::uuid IS NULL OR (s.name, s.set_id, -m.owner_version) > ($3, $4, -$5::bigint))
        ORDER BY s.name, s.set_id, m.owner_version DESC
        LIMIT $6;
        """;

    static string CurrentSql(string query) => query == Legacy ? LegacyBaseline : CatalogBaseline;
    static string BoundedSql(string query) => query == Legacy ? ConfigurationDbSql.GetParameterSets : ConfigurationDbSql.VersionPage;

    static Guid SetId(int number) => Guid.Parse(FormattableString.Invariant($"12345678-0000-0000-0000-{number:000000000000}"));
    static string Name(int number) => FormattableString.Invariant($"Group {(number - 1) / 2:D2}");

    static string ConnectionString(string? mode = null)
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        if (mode is not null)
            settings.Options = "-c plan_cache_mode=" + mode;
        return settings.ConnectionString;
    }

    static NpgsqlConnection Connection(string? mode = null) =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString(mode));

    static Cursor DeepCursor => new(Name(17), SetId(17), 250);
    static Cursor StartCursor => new("", null, 0);

    [GlobalSetup]
    public async Task Setup()
    {
        db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        current = Command(db, CurrentSql(Query), Deep ? DeepCursor : StartCursor, null, PageSize);
        bounded = Command(db, BoundedSql(Query), Deep ? DeepCursor : StartCursor, null, PageSize);
        await current.PrepareAsync().ConfigureAwait(false);
        await bounded.PrepareAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)] public Task<List<PageRow>> Current() => Read(current, Query);
    [Benchmark] public Task<List<PageRow>> PerSetProbe() => Read(bounded, Query);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await current.DisposeAsync().ConfigureAwait(false);
        await bounded.DisposeAsync().ConfigureAwait(false);
        await db.DisposeAsync().ConfigureAwait(false);
    }

    static NpgsqlCommand Command(NpgsqlConnection db, string sql, Cursor cursor, Guid? set, int limit, string component = Component)
    {
        var cmd = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(cmd, Values(Text(component), Uuid(set), Text(cursor.Name), Uuid(cursor.Set), cursor.Version, limit));
        return cmd;
    }

    static async Task<List<PageRow>> Read(NpgsqlCommand cmd, string query)
    {
        var rows = new List<PageRow>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
            rows.Add(query == Legacy
                ? new(reader.GetString(0), Guid.Empty, 0, reader.GetString(1), reader.GetString(2), reader.GetInt64(3))
                : new(null, reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4)));
        return rows;
    }

    internal static async Task PrepareAsync()
    {
        var settings = new DbConnectionSettings().Add(ConfigurationDbContext.ConfigurationDbConnection, ConnectionString(), "System.Data.Postgres");
        await ConfigurationSchemaDb.WithoutLookupDefaults(settings, NullLogger<DbProvider>.Instance).CreateAllAsync().ConfigureAwait(false);
        await using var db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        await Execute(db, $"""
            CREATE TEMP TABLE benchmark_sets AS
            SELECT n, ('12345678-0000-0000-0000-'||lpad(n::text,12,'0'))::uuid AS id,
                'Group '||lpad(((n-1)/2)::text,2,'0') AS name
            FROM generate_series(1,{Sets + 2}) n;
            INSERT INTO reference_configuration.parameter_set
            SELECT id, CASE WHEN n={Sets + 2} THEN 'other' ELSE '{Component}' END,name,'Fixture '||n,n
            FROM benchmark_sets;
            INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
            SELECT md5(code)::uuid,code,md5('parameter-area:strategy-workflow')::uuid,code,code
            FROM (VALUES ('{Component}'),('other')) fixtures(code);
            INSERT INTO reference_configuration.parameter_schema_version
            SELECT code,1,'fixture',repeat('a',64),jsonb_build_object()
            FROM (VALUES ('{Component}'),('other')) fixtures(code);
            INSERT INTO reference_configuration.parameter_set_version(set_id,version,payload_sha256,status,body)
            SELECT id,v,repeat('a',64),1,jsonb_build_object('Reference',
                jsonb_build_object('SetId',id,'Version',v,'PayloadSha256',repeat('a',64),
                    'ComponentCode',CASE WHEN n={Sets + 2} THEN 'other' ELSE '{Component}' END),
                'Status',1,'SchemaVersion',1,'PayloadJson',repeat('x',256))
            FROM benchmark_sets CROSS JOIN generate_series(1,{Versions}) v WHERE n<={Sets} OR n={Sets + 2};
            INSERT INTO reference_configuration.parameter_projection_record
            SELECT 'parameter_catalog_version',set_id,version,repeat('a',64)
            FROM reference_configuration.parameter_set_version;
            INSERT INTO reference_configuration.parameter_catalog_version(owner_set_id,owner_version,row_path,ordinal)
            SELECT set_id,version,'$',0 FROM reference_configuration.parameter_set_version;
            """).ConfigureAwait(false);
        foreach (var table in new[] { "parameter_set", "parameter_set_version", "parameter_catalog_version" })
            await Execute(db, $"VACUUM (ANALYZE) reference_configuration.{table};").ConfigureAwait(false);

        foreach (var query in new[] { Legacy, Catalog })
        {
            await Verify(db, query, "first", StartCursor).ConfigureAwait(false);
            await Verify(db, query, "deep", DeepCursor).ConfigureAwait(false);
            await Verify(db, query, "within-set", DeepCursor, SetId(17)).ConfigureAwait(false);
            await Verify(db, query, "same-name-transition", new(Name(17), SetId(17), 5)).ConfigureAwait(false);
            await Verify(db, query, "name-transition", new(Name(18), SetId(18), 1)).ConfigureAwait(false);
            await Verify(db, query, "scoped-cursor-before", DeepCursor, SetId(18)).ConfigureAwait(false);
            await Verify(db, query, "scoped-cursor-after", DeepCursor, SetId(16)).ConfigureAwait(false);
            await Verify(db, query, "end", new(Name(Sets), SetId(Sets), 5)).ConfigureAwait(false);
            await Verify(db, query, "empty-set", StartCursor, SetId(Sets + 1)).ConfigureAwait(false);
            await Verify(db, query, "missing-component", StartCursor, component: "missing").ConfigureAwait(false);
            await Verify(db, query, "zero-limit", StartCursor, limit: 0).ConfigureAwait(false);
            foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
            {
                await Execute(db, $"SET plan_cache_mode={mode};").ConfigureAwait(false);
                foreach (var (name, sql) in new[] { ("current", CurrentSql(query)), ("bounded", BoundedSql(query)) })
                {
                    await Execute(db, $"PREPARE parameter_page(text,uuid,text,uuid,int,int) AS {sql}").ConfigureAwait(false);
                    await Explain(db, query, mode, name, "first", "NULL,'',NULL,0").ConfigureAwait(false);
                    await Explain(db, query, mode, name, "deep", $"NULL,'{DeepCursor.Name}','{DeepCursor.Set}',{DeepCursor.Version}").ConfigureAwait(false);
                    await Explain(db, query, mode, name, "scoped-deep", $"'{SetId(17)}','{DeepCursor.Name}','{DeepCursor.Set}',{DeepCursor.Version}").ConfigureAwait(false);
                    await Execute(db, "DEALLOCATE parameter_page;").ConfigureAwait(false);
                }
            }
            await Execute(db, "SET plan_cache_mode=force_generic_plan;").ConfigureAwait(false);
        }
    }

    static async Task Verify(NpgsqlConnection db, string query, string name, Cursor cursor, Guid? set = null,
        int limit = PageSize, string component = Component)
    {
        await using var before = Command(db, CurrentSql(query), cursor, set, limit, component);
        await using var after = Command(db, BoundedSql(query), cursor, set, limit, component);
        await before.PrepareAsync().ConfigureAwait(false);
        await after.PrepareAsync().ConfigureAwait(false);
        var original = await Read(before, query).ConfigureAwait(false);
        var candidate = await Read(after, query).ConfigureAwait(false);
        var expected = Enumerable.Range(1, Sets).Where(n => (set is null || SetId(n) == set) && component == Component)
            .SelectMany(n => Enumerable.Range(1, Versions).Reverse().Select(v => (Set: SetId(n), Version: (long)v, Name: Name(n))))
            .Where(row => cursor.Set is null || string.CompareOrdinal(row.Name, cursor.Name) > 0 ||
                (row.Name == cursor.Name && (row.Set.CompareTo(cursor.Set.Value) > 0 ||
                    (row.Set == cursor.Set && row.Version < cursor.Version))))
            .Take(limit).ToArray();
        if (!original.SequenceEqual(candidate) || !candidate.Select(row => CursorFor(row)).SequenceEqual(expected))
            throw new InvalidOperationException($"Ordered page differed for {query} {name}.");
        if (candidate.Count != 0)
        {
            var last = CursorFor(original[^1]);
            var next = new Cursor(last.Name, last.Set, checked((int)last.Version));
            await using var beforeNext = Command(db, CurrentSql(query), next, set, limit, component);
            await using var afterNext = Command(db, BoundedSql(query), next, set, limit, component);
            if (!(await Read(beforeNext, query).ConfigureAwait(false)).SequenceEqual(await Read(afterNext, query).ConfigureAwait(false)))
                throw new InvalidOperationException($"Baseline-generated next cursor differed for {query} {name}.");
        }
        Console.WriteLine($"ORDERED,{query},{name},{candidate.Count},existing cursor and next page verified");
    }

    static (Guid Set, long Version, string Name) CursorFor(PageRow row)
    {
        if (row.Body is null)
            return (row.Set, row.Version, row.Name);
        using var doc = System.Text.Json.JsonDocument.Parse(row.Body);
        var reference = doc.RootElement.GetProperty("Reference");
        return (reference.GetProperty("SetId").GetGuid(), reference.GetProperty("Version").GetInt64(), row.Name);
    }

    static async Task Explain(NpgsqlConnection db, string query, string mode, string variant, string page, string args)
    {
        Console.WriteLine($"PLAN,{query},{mode},{variant},{page}");
        await using var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS) EXECUTE parameter_page('{Component}',{args},{PageSize})", db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        Console.WriteLine(string.Join(Environment.NewLine, lines));
        if (variant != "bounded")
            return;
        var key = query == Legacy ? "parameter_set_version_pkey" : "parameter_catalog_version_pkey";
        var owner = query == Legacy ? "set_id" : "owner_set_id";
        var version = query == Legacy ? "version" : "owner_version";
        if (!lines.Any(line => line.Contains($"Scan Backward using {key}", StringComparison.Ordinal)) ||
            !lines.Any(line => line.Contains("Index Cond:", StringComparison.Ordinal)
                && line.Contains($"{owner} = s.set_id", StringComparison.Ordinal)
                && line.Contains($"{version} <= CASE", StringComparison.Ordinal)))
            throw new InvalidOperationException($"Expected a bounded backward version probe for {query} {mode} {page}.");
        Console.WriteLine($"VERIFIED-PLAN,{query},{mode},{page},set and version bound in index condition");
    }

    static async Task Execute(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    sealed record Cursor(string Name, Guid? Set, int Version);
    public sealed record PageRow(string? Body, Guid Set, long Version, string Name, string Description, long Revision);
}
