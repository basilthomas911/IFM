using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

public sealed class SecuritiesDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_write_and_rollover_capabilities()
    {
        Assert.True(typeof(IObjectRepository<SecuritiesDbContext>).IsAssignableFrom(typeof(ISecuritiesDbContext)));
        Assert.True(typeof(ISecuritiesDbReadContext).IsAssignableFrom(typeof(ISecuritiesDbContext)));
        Assert.True(typeof(ICurrentFuturesContractCatalog).IsAssignableFrom(typeof(ISecuritiesDbReadContext)));
        Assert.True(typeof(IOptionPricingConventionStore).IsAssignableFrom(typeof(ISecuritiesDbReadContext)));
        Assert.True(typeof(ISecuritiesDbWriteContext).IsAssignableFrom(typeof(ISecuritiesDbContext)));
        Assert.True(typeof(IFuturesContractRolloverStore).IsAssignableFrom(typeof(ISecuritiesDbContext)));
        Assert.True(typeof(ISecuritiesDbContext).IsAssignableFrom(typeof(SecuritiesDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_securities_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.SecuritiesDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(ISecuritiesDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_contract_implementations()
    {
        Type[] contracts = [typeof(ISecuritiesDbReadContext), typeof(ISecuritiesDbWriteContext), typeof(IFuturesContractRolloverStore)];
        var contractMethods = contracts.SelectMany(type => type.GetMethods()).ToArray();
        var publicMethods = typeof(SecuritiesDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(SecuritiesDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(SecuritiesDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.SecuritiesDb.SecuritiesDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
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
        var context = File.ReadAllText(Path.Combine(folder, "SecuritiesDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "SecuritiesDbContextExtensions.cs"));

        Assert.DoesNotContain(source, line => line.Contains("partial class", StringComparison.Ordinal));
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(SecuritiesDbContext context)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_results_and_reference_codec_are_owned_by_the_shared_domain()
    {
        var storage = typeof(SecuritiesDbContext).Assembly;
        var shared = typeof(SecuritiesProjectionBackfillResult).Assembly;

        Assert.Equal("TomasAI.IFM.Domain.MarketData.Shared", shared.GetName().Name);
        Assert.NotEqual(storage, shared);
        Assert.Equal(shared, typeof(SecuritiesProjectionReconciliationResult).Assembly);
        Assert.Equal(shared, typeof(ReferencePayloadCodec).Assembly);
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.SecuritiesDb.SecuritiesProjectionBackfillResult"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.SecuritiesDb.ReferencePayloadCodec"));
    }

    [Fact]
    public void Reference_versions_are_context_contracts_with_shared_models_and_no_standalone_store()
    {
        var storage = typeof(SecuritiesDbContext).Assembly;
        var shared = typeof(ReferenceContractVersion).Assembly;

        Assert.Equal("TomasAI.IFM.Domain.Reference.Shared", shared.GetName().Name);
        Assert.Equal(shared, typeof(PendingReferenceVersion).Assembly);
        Assert.Equal(shared, typeof(ReferenceContractVersionSummaryReadModel).Assembly);
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.SecuritiesDb.ReferenceVersionStore"));
        Assert.Contains(typeof(ISecuritiesDbReadContext).GetMethods(), method =>
            method.Name == nameof(ISecuritiesDbReadContext.GetReferenceVersionAsync));
        Assert.Contains(typeof(ISecuritiesDbWriteContext).GetMethods(), method =>
            method.Name == nameof(ISecuritiesDbWriteContext.StageReferenceVersionAsync));
    }

    [Fact]
    public void Current_futures_catalog_is_the_read_context_not_a_pass_through_adapter()
    {
        var storage = typeof(SecuritiesDbContext).Assembly;

        Assert.True(typeof(ICurrentFuturesContractCatalog).IsAssignableFrom(typeof(SecuritiesDbContext)));
        Assert.Null(storage.GetType(
            "TomasAI.IFM.Application.Storage.SecuritiesDb.SecuritiesCurrentFuturesContractCatalog"));
        var catalogMethod = typeof(ICurrentFuturesContractCatalog).GetMethod(
            nameof(ICurrentFuturesContractCatalog.GetFuturesContractsBySymbolAsync));
        Assert.NotNull(catalogMethod);
        Assert.Contains(typeof(ISecuritiesDbReadContext).GetMethods(), method =>
            SameSignature(catalogMethod, method));
    }

    [Fact]
    public void Folder_contains_only_securities_database_implementation_concerns()
    {
        var storage = typeof(SecuritiesDbContext).Assembly;

        Assert.Null(storage.GetType(
            "TomasAI.IFM.Application.Storage.SecuritiesDb.SecuritiesOptionPricingConventionStore"));
        Assert.Null(storage.GetType(
            "TomasAI.IFM.Application.Storage.OptionPricing.ReferenceVersionOptionPricingConventionStore"));
        Assert.DoesNotContain(
            Directory.GetFiles(GetFolder(), "*.cs", SearchOption.AllDirectories),
            file => Path.GetFileName(file).Contains("OptionPricingConventionStore", StringComparison.Ordinal));
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        string[] boundaries =
        [
            ").SetParameters(", ").ExecuteCommandAsync(", ").ExecuteSingleAsync(", ").ExecuteQueryAsync(",
            ").ExecuteScalarAsync(", ").QueueCommand(", ").ConfigureAwait("
        ];

        foreach (var file in Directory.GetFiles(GetFolder(), "*.cs", SearchOption.AllDirectories))
            Assert.DoesNotContain(File.ReadAllLines(file), line =>
                boundaries.Any(boundary => line.Contains(boundary, StringComparison.Ordinal)));
    }

    private static bool SameSignature(MethodInfo contract, MethodInfo implementation)
        => contract.Name == implementation.Name
            && contract.ReturnType == implementation.ReturnType
            && contract.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(implementation.GetParameters().Select(parameter => parameter.ParameterType));

    private static string GetFolder()
        => Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "SecuritiesDb");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
