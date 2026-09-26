using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataServiceDb;

public sealed class MarketDataServiceDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<MarketDataServiceDbContext>).IsAssignableFrom(typeof(IMarketDataServiceDbContext)));
        Assert.True(typeof(IMarketDataServiceDbReadContext).IsAssignableFrom(typeof(IMarketDataServiceDbContext)));
        Assert.True(typeof(IMarketDataServiceDbWriteContext).IsAssignableFrom(typeof(IMarketDataServiceDbContext)));
        Assert.True(typeof(IMarketDataServiceStore).IsAssignableFrom(typeof(IMarketDataServiceDbContext)));
        Assert.True(typeof(ICompositionRoutePlanStore).IsAssignableFrom(typeof(IMarketDataServiceDbContext)));
        Assert.True(typeof(IDurableSubscriptionIntentStore).IsAssignableFrom(typeof(IMarketDataServiceDbContext)));
        Assert.True(typeof(IMarketDataServiceDbContext).IsAssignableFrom(typeof(MarketDataServiceDbContext)));
    }

    [Fact]
    public void Legacy_standalone_market_data_service_stores_do_not_exist()
    {
        var assembly = typeof(MarketDataServiceDbContext).Assembly;

        Assert.Null(assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions.PostgresCompositionRoutePlanStore"));
        Assert.Null(assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions.PostgresDurableSubscriptionIntentStore"));
        Assert.Null(assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions.MarketDataServiceDurableSubscriptionStore"));
    }

    [Fact]
    public void Factory_exposes_the_typed_market_data_service_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.MarketDataServiceDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IMarketDataServiceDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_read_or_write_contract_implementations()
    {
        var contractMethods = new[] { typeof(IMarketDataServiceDbReadContext), typeof(IMarketDataServiceDbWriteContext) }
            .SelectMany(type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(MarketDataServiceDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_no_instance_helpers()
    {
        var declared = typeof(MarketDataServiceDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(MarketDataServiceDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataServiceDb.MarketDataServiceDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(extensionMethods);
        Assert.All(extensionMethods, method =>
            Assert.True(method.IsDefined(typeof(ExtensionAttribute), inherit: false), $"{method.Name} must be declared as an extension method."));
    }

    [Fact]
    public void Context_is_non_partial_and_extensions_use_csharp_14_blocks()
    {
        var root = FindRepositoryRoot();
        var context = File.ReadAllText(Path.Combine(root, "TomasAI.IFM.Application.Storage", "MarketDataServiceDb", "MarketDataServiceDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(root, "TomasAI.IFM.Application.Storage", "MarketDataServiceDb", "MarketDataServiceDbContextExtensions.cs"));

        Assert.DoesNotContain("partial class MarketDataServiceDbContext", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(MarketDataServiceDbContext context)", extensions, StringComparison.Ordinal);
        Assert.Contains("extension(FuturesRolloverContractAssignment assignment)", extensions, StringComparison.Ordinal);
        Assert.Contains("extension(DateTime value)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);

        var lines = File.ReadAllLines(Path.Combine(root, "TomasAI.IFM.Application.Storage", "MarketDataServiceDb", "MarketDataServiceDbContextExtensions.cs"));
        var declarations = lines
            .Select((text, index) => (Text: text, Index: index))
            .Where(line => line.Text.StartsWith("        internal ", StringComparison.Ordinal)
                && line.Text.Contains('('))
            .ToArray();
        Assert.NotEmpty(declarations);
        Assert.All(declarations, declaration =>
        {
            var documentation = lines
                .Take(declaration.Index)
                .Reverse()
                .TakeWhile(line => line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                .ToArray();
            Assert.Contains(documentation, line =>
                line.Contains("<summary>", StringComparison.Ordinal));
            Assert.Contains(documentation, line =>
                line.Contains("</summary>", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        var root = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(root, "TomasAI.IFM.Application.Storage", "MarketDataServiceDb", "MarketDataServiceDbContext.cs"),
            Path.Combine(root, "TomasAI.IFM.Application.Storage", "MarketDataServiceDb", "MarketDataServiceDbContextExtensions.cs")
        ];
        string[] boundaries =
        [
            ").SetParameters(", ").ExecuteCommandAsync(", ").ExecuteSingleAsync(", ").ExecuteQueryAsync(",
            ").ExecuteScalarAsync(", ").QueueCommand(", ").ConfigureAwait("
        ];

        foreach (var file in files)
            Assert.DoesNotContain(File.ReadAllLines(file), line =>
                boundaries.Any(boundary => line.Contains(boundary, StringComparison.Ordinal)));
    }

    private static bool SameSignature(MethodInfo contract, MethodInfo implementation)
        => contract.Name == implementation.Name
            && contract.ReturnType == implementation.ReturnType
            && contract.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(implementation.GetParameters().Select(parameter => parameter.ParameterType));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
