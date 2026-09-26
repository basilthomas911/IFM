using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.PortfolioDb;

public sealed class PortfolioDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<PortfolioDbContext>).IsAssignableFrom(typeof(IPortfolioDbContext)));
        Assert.True(typeof(IPortfolioDbReadContext).IsAssignableFrom(typeof(IPortfolioDbContext)));
        Assert.True(typeof(IPortfolioDbWriteContext).IsAssignableFrom(typeof(IPortfolioDbContext)));
        Assert.True(typeof(IPortfolioDbContext).IsAssignableFrom(typeof(PortfolioDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_portfolio_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.PortfolioDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IPortfolioDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_read_or_write_contract_implementations()
    {
        var contractMethods = new[] { typeof(IPortfolioDbReadContext), typeof(IPortfolioDbWriteContext) }
            .SelectMany(type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(PortfolioDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(PortfolioDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(PortfolioDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName
                && !method.Name.StartsWith("get_", StringComparison.Ordinal)
                && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();
        Assert.NotEmpty(extensionMethods);
        Assert.All(extensionMethods, method =>
            Assert.True(
                method.IsDefined(typeof(ExtensionAttribute), inherit: false),
                $"{method.Name} must be declared as an extension method."));
    }

    [Fact]
    public void Context_is_non_partial_validation_free_and_extensions_use_csharp_14_blocks()
    {
        var root = FindRepositoryRoot();
        var folder = Path.Combine(root, "TomasAI.IFM.Application.Storage", "PortfolioDb");
        var context = File.ReadAllText(Path.Combine(folder, "PortfolioDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "PortfolioDbContextExtensions.cs"));

        Assert.DoesNotContain("partial class PortfolioDbContext", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(PortfolioDbContext context)", extensions, StringComparison.Ordinal);
        Assert.Contains("extension<T>(PortfolioProjection<T> projection)", extensions, StringComparison.Ordinal);
        Assert.Contains("extension(object?[] values)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Projection_models_are_owned_by_the_portfolio_shared_domain()
    {
        var storage = typeof(PortfolioDbContext).Assembly;
        var shared = typeof(PortfolioProjectionRevision).Assembly;

        Assert.NotEqual(storage, shared);
        Assert.Equal("TomasAI.IFM.Domain.Portfolio.Shared", shared.GetName().Name);
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioProjectionRevision"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioDb.DraftPortfolioProjectionDeletion"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioDb.DraftPolicyProjectionDeletion"));
        Assert.Equal(shared, typeof(CapacityExpiryDispatch).Assembly);
        Assert.Equal(shared, typeof(FinancialAuthorityPreparationSnapshot).Assembly);
        Assert.Equal(shared, typeof(FinancialDevelopmentPolicy).Assembly);
        Assert.Equal(shared, typeof(LedgerCommitInfo).Assembly);
        Assert.NotEqual(storage, typeof(FinancialWorkflowRecoveryPage).Assembly);
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioFinancial.CapacityExpiryDispatch"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialAuthorityPreparationSnapshot"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialDevelopmentPolicy"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioFinancial.FinancialWorkflowRecoveryPage"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.PortfolioFinancial.LedgerCommitInfo"));
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        var root = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(root, "TomasAI.IFM.Application.Storage", "PortfolioDb", "PortfolioDbContext.cs"),
            Path.Combine(root, "TomasAI.IFM.Application.Storage", "PortfolioDb", "PortfolioDbContextExtensions.cs")
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
        => contract.ToString() == implementation.ToString();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
