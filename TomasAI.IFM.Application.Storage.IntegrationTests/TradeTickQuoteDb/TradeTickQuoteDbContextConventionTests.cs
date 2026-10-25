using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeTickQuoteDb;

public sealed class TradeTickQuoteDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<TradeTickQuoteDbContext>)
            .IsAssignableFrom(typeof(ITradeTickQuoteDbContext)));
        Assert.True(typeof(ITradeTickQuoteDbReadContext)
            .IsAssignableFrom(typeof(ITradeTickQuoteDbContext)));
        Assert.True(typeof(ITradeTickQuoteDbWriteContext)
            .IsAssignableFrom(typeof(ITradeTickQuoteDbContext)));
        Assert.True(typeof(ITradeTickQuoteDbContext)
            .IsAssignableFrom(typeof(TradeTickQuoteDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_trade_plan_context()
    {
        var property = typeof(IDbContextFactory)
            .GetProperty(nameof(IDbContextFactory.TradeTickQuoteDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(ITradeTickQuoteDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_contract_implementations()
    {
        Type[] contracts =
        [
            typeof(ITradeTickQuoteDbReadContext),
            typeof(ITradeTickQuoteDbWriteContext)
        ];
        var contractMethods = contracts
            .SelectMany(static type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(TradeTickQuoteDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .ToArray();

        Assert.All(
            publicMethods,
            implementation => Assert.Contains(
                contractMethods,
                contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(TradeTickQuoteDbContext)
            .GetMethods(
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.DeclaredOnly)
            .Where(static method =>
                !method.IsSpecialName
                && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(
            declared.Where(static method => method.IsStatic),
            method => Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(
            declared,
            static method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(TradeTickQuoteDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.TradeTickQuoteDb.TradeTickQuoteDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions
            .GetMethods(
                BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly)
            .Where(static method =>
                !method.IsSpecialName
                && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();
        Assert.NotEmpty(extensionMethods);
        Assert.All(
            extensionMethods,
            method => Assert.True(
                method.IsDefined(typeof(ExtensionAttribute), false),
                $"{method.Name} must be an extension method."));
    }

    [Fact]
    public void Folder_has_no_partial_context_implementations_or_context_argument_validation()
    {
        var folder = GetFolder();
        var source = Directory
            .GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadAllLines);
        var context = File.ReadAllText(
            Path.Combine(folder, "TradeTickQuoteDbContext.cs"));
        var extensions = File.ReadAllText(
            Path.Combine(folder, "TradeTickQuoteDbContextExtensions.cs"));

        Assert.DoesNotContain(
            source,
            line => line.Contains(
                "partial class TradeTickQuoteDbContext",
                StringComparison.Ordinal));
        Assert.DoesNotContain("throw new Argument", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains(
            "extension(TradeTickQuoteDbContext context)",
            extensions,
            StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Observation_contracts_are_owned_by_market_data_feed_shared()
    {
        var storage = typeof(TradeTickQuoteDbContext).Assembly;
        var shared = typeof(FuturesTickTradeDataInsertedEvent).Assembly;

        Assert.Equal("TomasAI.IFM.Application.Contracts.MarketData.Feed", shared.GetName().Name);
        Assert.Equal(shared, typeof(FuturesTickQuoteDataInsertedEvent).Assembly);
        Assert.NotEqual(storage, shared);
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        string[] boundaries =
        [
            ").SetParameters(",
            ").ExecuteCommandAsync(",
            ").ExecuteSingleAsync(",
            ").ExecuteQueryAsync(",
            ").ExecuteScalarAsync(",
            ").ExecutePageAsync(",
            ").QueueCommand(",
            ").ConfigureAwait("
        ];

        foreach (var file in Directory.GetFiles(
                     GetFolder(),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(
                File.ReadAllLines(file),
                line => boundaries.Any(
                    boundary => line.Contains(boundary, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void Folder_has_one_helper_binding_catalog_and_scylla_schema()
    {
        string[] expected = ["ITradeTickQuoteDbContext.cs", "ITradeTickQuoteDbReadContext.cs", "ITradeTickQuoteDbWriteContext.cs", "TradeTickQuoteDbContext.cs", "TradeTickQuoteDbContextExtensions.cs", "TradeTickQuoteDbParameters.cs", "TradeTickQuoteDbCql.cs", Path.Combine("Schema", "TradeTickQuoteSchemaDb.cs"), Path.Combine("Schema", "TradeTickQuoteSchemaCql.cs")];
        var folder = GetFolder();
        var actual = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(folder, p)).OrderBy(p => p).ToArray();
        Assert.Equal(expected.OrderBy(p => p).ToArray(), actual);
        var schema = File.ReadAllText(Path.Combine(folder, "Schema", "TradeTickQuoteSchemaDb.cs"));
        Assert.Contains("TradeTickQuoteSchemaCql.Definitions", schema, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderName", schema, StringComparison.Ordinal);
    }


    private static bool SameSignature(
        MethodInfo contract,
        MethodInfo implementation) =>
        contract.Name == implementation.Name
        && contract.ReturnType == implementation.ReturnType
        && contract
            .GetParameters()
            .Select(static parameter => parameter.ParameterType)
            .SequenceEqual(
                implementation
                    .GetParameters()
                    .Select(static parameter => parameter.ParameterType));

    private static string GetFolder() =>
        Path.Combine(
            FindRepositoryRoot(),
            "TomasAI.IFM.Application.Storage",
            "TradeTickQuoteDb");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root.");
    }
}
