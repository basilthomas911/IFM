using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

public sealed class TradeDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<TradeDbContext>).IsAssignableFrom(typeof(ITradeDbContext)));
        Assert.True(typeof(ITradeDbReadContext).IsAssignableFrom(typeof(ITradeDbContext)));
        Assert.True(typeof(ITradeDbWriteContext).IsAssignableFrom(typeof(ITradeDbContext)));
        Assert.True(typeof(ITradeDbContext).IsAssignableFrom(typeof(TradeDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_trade_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.TradeDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(ITradeDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_contract_implementations()
    {
        var contracts = typeof(TradeDbContext).GetInterfaces();
        var contractMethods = contracts.SelectMany(static type => type.GetMethods()).ToArray();
        var publicMethods = typeof(TradeDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(TradeDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(static method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, static method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(TradeDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.TradeDb.TradeDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();
        Assert.NotEmpty(extensionMethods);
        Assert.All(extensionMethods, method =>
            Assert.True(method.IsDefined(typeof(ExtensionAttribute), false), $"{method.Name} must be an extension method."));
    }

    [Fact]
    public void Folder_has_no_partial_context_implementations_or_context_argument_validation()
    {
        var folder = GetFolder();
        var source = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).SelectMany(File.ReadAllLines);
        var context = File.ReadAllText(Path.Combine(folder, "TradeDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "TradeDbContextExtensions.cs"));

        Assert.DoesNotContain(source, line => line.Contains("partial class TradeDbContext", StringComparison.Ordinal));
        Assert.DoesNotContain("throw new Argument", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(TradeDbContext context)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_results_are_owned_by_the_trade_shared_domain()
    {
        var storage = typeof(TradeDbContext).Assembly;
        var shared = typeof(OpenPositionRouteReadModel).Assembly;

        Assert.Equal("TomasAI.IFM.Application.Contracts.Trade", shared.GetName().Name);
        Assert.Equal(shared, typeof(OpenPositionRouteKeyReadModel).Assembly);
        Assert.Equal(shared, typeof(RiskHistoryProjectionResult).Assembly);
        Assert.NotEqual(storage, shared);
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.TradeDb.RiskHistoryProjectionResult"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.TradeDb.OpenPositionRouteReadModel"));
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        string[] boundaries =
        [
            ").SetParameters(", ").ExecuteCommandAsync(", ").ExecuteSingleAsync(", ").ExecuteQueryAsync(",
            ").ExecuteScalarAsync(", ").ExecutePageAsync(", ").QueueCommand(", ").ConfigureAwait("
        ];

        foreach (var file in Directory.GetFiles(GetFolder(), "*.cs", SearchOption.AllDirectories))
            Assert.DoesNotContain(File.ReadAllLines(file), line =>
                boundaries.Any(boundary => line.Contains(boundary, StringComparison.Ordinal)));
    }

    [Fact]
    public void Folder_has_one_binding_helper_and_postgres_schema_catalog()
    {
        var folder = GetFolder();
        string[] expected =
        [
            "IEnlistedTradeDbWriter.cs", "ITradeDbContext.cs", "ITradeDbReadContext.cs",
            "ITradeDbWriteContext.cs", "TradeDbContext.cs", "TradeDbContextExtensions.cs",
            "TradeDbParameters.cs", "TradeDbSql.cs",
            Path.Combine("Schema", "TradeSchemaDb.cs"), Path.Combine("Schema", "TradeSchemaSql.cs")
        ];
        var actual = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder, path)).OrderBy(path => path).ToArray();
        Assert.Equal(expected.OrderBy(path => path).ToArray(), actual);
        var context = File.ReadAllText(Path.Combine(folder, "TradeDbContext.cs"));
        var schema = File.ReadAllText(Path.Combine(folder, "Schema", "TradeSchemaDb.cs"));
        Assert.DoesNotContain("_dbFactory", context, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Data.ScyllaDb", schema, StringComparison.Ordinal);
        Assert.Contains("TradeSchemaSql.Create", schema, StringComparison.Ordinal);
        Assert.Contains("TradeSchemaSql.Drop", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void Typed_write_contract_includes_caller_owned_projection_transactions()
    {
        Assert.True(typeof(IEnlistedTradeDbWriter).IsAssignableFrom(typeof(ITradeDbWriteContext)));
        Assert.True(typeof(IEnlistedTradeDbWriter).IsAssignableFrom(typeof(ITradeDbContext)));
    }

    [Fact]
    public void Native_workflow_reads_bind_one_execution_parameter_set()
    {
        var context = File.ReadAllText(Path.Combine(GetFolder(), "TradeDbContext.cs"));
        Assert.DoesNotContain(".SetParameters(Values(", context, StringComparison.Ordinal);
        Assert.Equal(6, context.Split(".SetParameters(new TradeDbValues(Values(", StringSplitOptions.None).Length - 1);
    }

    private static bool SameSignature(MethodInfo contract, MethodInfo implementation)
        => contract.Name == implementation.Name
            && contract.ReturnType == implementation.ReturnType
            && contract.GetParameters().Select(static parameter => parameter.ParameterType)
                .SequenceEqual(implementation.GetParameters().Select(static parameter => parameter.ParameterType));

    private static string GetFolder()
        => Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "TradeDb");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
