using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

public sealed class ReferenceDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<ReferenceDbContext>).IsAssignableFrom(typeof(IReferenceDbContext)));
        Assert.True(typeof(IReferenceDbReadContext).IsAssignableFrom(typeof(IReferenceDbContext)));
        Assert.True(typeof(IReferenceDbWriteContext).IsAssignableFrom(typeof(IReferenceDbContext)));
        Assert.True(typeof(IReferenceDbContext).IsAssignableFrom(typeof(ReferenceDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_reference_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.ReferenceDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IReferenceDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_read_or_write_contract_implementations()
    {
        var contractMethods = new[] { typeof(IReferenceDbReadContext), typeof(IReferenceDbWriteContext) }
            .SelectMany(type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(ReferenceDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => contract.ToString() == implementation.ToString()));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(ReferenceDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(ReferenceDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.ReferenceDb.ReferenceDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();
        Assert.NotEmpty(extensionMethods);
        Assert.All(extensionMethods, method =>
            Assert.True(method.IsDefined(typeof(ExtensionAttribute), inherit: false)));
    }

    [Fact]
    public void Folder_has_no_partial_implementations_and_context_has_no_argument_validation()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "ReferenceDb");
        var source = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadAllLines)
            .ToArray();
        var context = File.ReadAllText(Path.Combine(folder, "ReferenceDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "ReferenceDbContextExtensions.cs"));

        Assert.DoesNotContain(source, line => line.Contains("partial class", StringComparison.Ordinal));
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(ReferenceDbContext context)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_query_results_are_owned_by_the_shared_domain()
    {
        var storage = typeof(ReferenceDbContext).Assembly;
        var shared = typeof(ReferenceProjectionBackfillResult).Assembly;

        Assert.Equal("TomasAI.IFM.Domain.Reference.Shared", shared.GetName().Name);
        Assert.NotEqual(storage, shared);
        Assert.Equal(shared, typeof(ReferenceProjectionReconciliationResult).Assembly);
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.ReferenceDb.ReferenceProjectionBackfillResult"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.ReferenceDb.ReferenceProjectionReconciliationResult"));
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "ReferenceDb");
        string[] files =
        [
            Path.Combine(folder, "ReferenceDbContext.cs"),
            Path.Combine(folder, "ReferenceDbContextExtensions.cs")
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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
