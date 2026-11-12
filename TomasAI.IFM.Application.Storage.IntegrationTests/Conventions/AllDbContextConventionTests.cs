using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.Conventions;

public sealed class AllDbContextConventionTests
{
    public static IEnumerable<object[]> Cases => StorageConventionAudit.Contexts.SelectMany(context => StorageConventionAudit.Rules.Select(rule => new object[] { context.Name, rule }));

    [Fact]
    public void Named_mapper_forwarding_and_map_reduce_reducers_are_not_inline_record_mappers()
    {
        var source = StorageSource.Parse("ExampleDbContext.cs", """
            class ExampleDbContext {
                void Read() {
                    query.ExecuteQueryAsync(row => MapToResult(row, contract));
                    query.ExecuteMapReduceAsync(MapToResult, rows => total += rows.Count());
                    query.ExecuteQueryAsync(row => new Result(row.GetInt(0)));
                    query.ExecuteQueryAsync(row => MapToResult(row.GetInt(0), contract));
                }
            }
            """);
        Assert.Equal(2, StorageConventionAudit.Inspect(
            new("ExampleDb", "ExampleDbContext", "Example"), "Mappers", [source]).Count);
    }

    [Fact]
    public void Inventory_covers_every_concrete_runtime_repository_regardless_of_filename()
    {
        var actual = typeof(DbContextFactory).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !typeof(IDbSchemaContext).IsAssignableFrom(type)
                // This helper is bound to a transaction owned by MarketDataServiceDbContext.
                && type != typeof(MarketDataServiceTransactionRepository)
                && DerivesFromObjectDataRepository(type))
            .Select(type => type.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(StorageConventionAudit.Contexts.Select(context => context.Context).Order(StringComparer.Ordinal), actual);
        Assert.Equal(13, actual.Length);
    }

    private static bool DerivesFromObjectDataRepository(Type type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ObjectDataRepository<>))
                return true;
        return false;
    }

    [Fact]
    public void Complete_report_retains_every_context_rule_and_individual_finding()
    {
        var report = StorageConventionAudit.Contexts.Select(spec => new
        {
            Context = spec.Name,
            Checks = StorageConventionAudit.Rules.Select(rule => new { Rule = rule, Findings = StorageConventionAudit.Inspect(spec, rule) }).ToArray()
        }).ToArray();
        Assert.Equal(13, report.Length);
        Assert.All(report, entry => Assert.Equal(StorageConventionAudit.Rules.Length, entry.Checks.Length));
        if (Environment.GetEnvironmentVariable("IFM_STORAGE_CONVENTION_REPORT") is { Length: > 0 } path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void Domain_ownership_cannot_be_hidden_elsewhere_in_the_Storage_project()
    {
        var root = Path.Combine(DbContextConventionSource.RepositoryRoot, "TomasAI.IFM.Application.Storage");
        var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .Select(path => StorageSource.Parse(path, File.ReadAllText(path))).ToArray();
        var issues = StorageConventionAudit.Inspect(new("StorageBoundary", "StorageBoundaryDbContext", "Storage"), "Ownership", sources);
        Assert.True(issues.Count == 0, string.Join("\n", issues));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_context_and_supporting_folder_follows_each_convention(string context, string rule)
    {
        var spec = StorageConventionAudit.Contexts.Single(spec => spec.Name == context);
        var findings = StorageConventionAudit.Inspect(spec, rule);
        Assert.True(findings.Count == 0, $"{context}/{rule}: {findings.Count} finding(s).\n" + string.Join("\n", findings));
    }

    [Theory]
    [InlineData("Helpers")]
    [InlineData("Mappers")]
    [InlineData("Statements")]
    [InlineData("FluentCalls")]
    [InlineData("Documentation")]
    public void Shared_schema_infrastructure_follows_the_same_implementation_boundaries(string rule)
    {
        var root = Path.Combine(DbContextConventionSource.RepositoryRoot, "TomasAI.IFM.Application.Storage", "SchemaDb");
        var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => StorageSource.Parse(path, File.ReadAllText(path))).ToArray();
        var issues = StorageConventionAudit.Inspect(new("SchemaDb", "SchemaDbContext", "Schema"), rule, sources);
        Assert.True(issues.Count == 0, string.Join("\n", issues));
    }

    [Fact]
    public void Auditor_reports_all_helper_validation_and_event_creation_occurrences()
    {
        var spec = new StorageContextSpec("ExampleDb", "ExampleDbContext", "Example");
        var source = StorageSource.Parse("ExampleDbContextExtensions.cs", """
            class ExampleDbContextExtensions {
              void ValidateRequiredArgument(object first) { ArgumentNullException.ThrowIfNull(first); }
              void Store(object second) { ValidateRequiredArgument(second); ArgumentException.ThrowIfNullOrWhiteSpace(second); }
              void HiddenPolicy() { var first = new FirstCreatedEvent(); var second = new SecondCreatedEvent(); }
            }
            """);
        Assert.Equal(3, StorageConventionAudit.Inspect(spec, "Validation", [source]).Count);
        Assert.Equal(2, StorageConventionAudit.Inspect(spec, "Ownership", [source]).Count);
    }

    [Fact]
    public void Auditor_flags_typed_application_cache_policy_even_in_supporting_files()
    {
        var source = StorageSource.Parse("ImmutableRows.cs", """
            class ImmutableRows {
              readonly AsyncReadCache<string, Row> exactVersions = new(512, TimeSpan.FromHours(1));
            }
            """);
        var finding = Assert.Single(StorageConventionAudit.Inspect(
            new("ExampleDb", "ExampleDbContext", "Example"), "Ownership", [source]));
        Assert.Contains("cache policy", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void Auditor_preserves_constructor_and_database_resource_integrity_guards()
    {
        var spec = new StorageContextSpec("ExampleDb", "ExampleDbContext", "Example");
        var source = StorageSource.Parse("ExampleDbContext.cs", """
            class ExampleDbContext {
              ExampleDbContext(object connection) { ArgumentNullException.ThrowIfNull(connection); }
              void Write(object transaction) { ArgumentNullException.ThrowIfNull(transaction); }
              void WriteEnlisted(IEnlistedPostgresTransaction enlisted) { ArgumentNullException.ThrowIfNull(enlisted); }
              void WriteQualified(EventSourceDb.IEnlistedPostgresTransaction enlisted) { ArgumentNullException.ThrowIfNull(enlisted); }
              void WriteRequest(WriteRequestDto enlisted) { ArgumentNullException.ThrowIfNull(enlisted); }
              static object MapToRow(object row) { ArgumentNullException.ThrowIfNull(row); return row; }
            }
            static class ExampleDbContextExtensions {
              extension(IEnlistedPostgresTransaction db) {
                void Write() { ArgumentNullException.ThrowIfNull(db); }
              }
            }
            """);
        var finding = Assert.Single(StorageConventionAudit.Inspect(spec, "Validation", [source]));
        Assert.Contains("Incoming validation", finding, StringComparison.Ordinal);
    }

    [Fact]
    public void Trade_cursor_decoder_retains_storage_fences_and_flags_the_request_bounds()
    {
        var source = StorageSource.Parse("TradeDbContextExtensions.cs", """
            static class TradeDbContextExtensions {
              extension(IEnlistedPostgresTransaction db) {
                void TradeReadPositionHistoryAsync(int pageSize, byte[]? cursor) {
                  if (pageSize < 1) throw new ArgumentException("Invalid request", nameof(pageSize));
                  if (cursor is not null) {
                    var last = JsonSerializer.Deserialize<TradeReadPositionCursor>(cursor);
                    if (last is null) throw new ArgumentException("Invalid cursor", nameof(cursor));
                  }
                }
              }
            }
            """);
        Assert.Single(StorageConventionAudit.Inspect(
            new("TradeDb", "TradeDbContext", "Trade"), "Validation", [source]));
    }

    [Fact]
    public void Projector_request_helpers_are_flagged_but_the_enlisted_executor_keeps_binding_guards()
    {
        var spec = new StorageContextSpec("ExampleDb", "ExampleDbContext", "Example");
        var source = StorageSource.Parse("ExampleDbContext.cs", """
            class EnlistedEventTransaction {
              void ExecuteAsync(object[] parameters) { ArgumentNullException.ThrowIfNull(parameters); }
            }
            class ExampleDbContext {
              void Write(object[] parameters, string name) {
                ArgumentNullException.ThrowIfNull(parameters);
                ValidateProjectorIdentity(1, name);
              }
            }
            """);
        Assert.Equal(2, StorageConventionAudit.Inspect(spec, "Validation", [source]).Count);
    }

    [Fact]
    public void Postgres_connection_and_executor_guards_do_not_exempt_caller_inputs()
    {
        var source = StorageSource.Parse("ExampleDbContext.cs", """
            record struct PostgresDatabaseIdentity {
              void Parse(string connectionString) { ArgumentException.ThrowIfNullOrWhiteSpace(connectionString); }
            }
            class PostgresEventTransaction {
              void ExecuteOnceAsync(object operation) { ArgumentNullException.ThrowIfNull(operation); }
              void WriteDraft(object operation) { ArgumentNullException.ThrowIfNull(operation); }
            }
            """);
        Assert.Single(StorageConventionAudit.Inspect(
            new("ExampleDb", "ExampleDbContext", "Example"), "Validation", [source]));
    }

    [Fact]
    public void Audit_flags_implicit_identity_allocation_outside_the_sequence_context()
    {
        var source = StorageSource.Parse("ExampleDbContext.cs", """
            class ExampleDbContext {
              void Write() { var id = sequence.GetSequenceIdAsync(name); }
              void Read() { var id = sequence.GetCurrentSequenceIdAsync(name); }
            }
            """);
        var finding = Assert.Single(StorageConventionAudit.Inspect(
            new("ExampleDb", "ExampleDbContext", "Example"), "Ownership", [source]));
        Assert.Contains("Allocate application identities", finding, StringComparison.Ordinal);
        Assert.Empty(StorageConventionAudit.Inspect(
            new("SequenceIdDb", "ExampleDbContext", "Example"), "Ownership", [source]));
    }

    [Fact]
    public void Auditor_rejects_inline_result_mappers_in_supporting_files()
    {
        var source = StorageSource.Parse("ExampleDbContextExtensions.cs", """
            static class ExampleDbContextExtensions {
              void Read() { db.ExecuteSingleAsync(row => row.GetString(0)); db.QueryAsync(sql, values, static row => row.GetLong(1), token); }
              static long MapToValue(IObjectDataRecord row) => row.GetLong(0);
            }
            """);
        Assert.Equal(2, StorageConventionAudit.Inspect(new("ExampleDb", "ExampleDbContext", "Example"), "Mappers", [source]).Count);
    }

    [Fact]
    public void Auditor_requires_documented_primary_constructor_arguments()
    {
        var source = StorageSource.Parse("ExampleDbContext.cs", """
            /// <summary>Persists accepted values.</summary>
            /// <param name="connection"></param>
            class ExampleDbContext(object connection) {}
            """);
        var findings = StorageConventionAudit.Inspect(new("ExampleDb", "ExampleDbContext", "Example"), "Documentation", [source]);
        Assert.Contains(findings, finding => finding.Contains("primary-constructor parameter documentation: connection", StringComparison.Ordinal));
    }

    [Fact]
    public void Auditor_recognizes_valid_CSharp14_blocks_and_rejects_ordinary_helpers()
    {
        var spec = new StorageContextSpec("ExampleDb", "ExampleDbContext", "Example");
        var source = StorageSource.Parse("ExampleDbContextExtensions.cs", """
            static class ExampleDbContextExtensions {
              extension(ExampleDbContext context) { void Bind() {} }
              static void OrdinaryHelper() {}
            }
            """);
        Assert.DoesNotContain(source.Root.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var issue = Assert.Single(StorageConventionAudit.Inspect(spec, "Helpers", [source]));
        Assert.Contains("ordinary static/legacy", issue, StringComparison.Ordinal);
    }
}
