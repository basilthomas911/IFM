using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SystemAdminDb;

public sealed class SystemAdminDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<SystemAdminDbContext>).IsAssignableFrom(typeof(ISystemAdminDbContext)));
        Assert.True(typeof(ISystemAdminDbReadContext).IsAssignableFrom(typeof(ISystemAdminDbContext)));
        Assert.True(typeof(ISystemAdminDbWriteContext).IsAssignableFrom(typeof(ISystemAdminDbContext)));
        Assert.True(typeof(ISystemAdminDbContext).IsAssignableFrom(typeof(SystemAdminDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_system_admin_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.SystemAdminDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(ISystemAdminDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_contract_implementations()
    {
        Type[] contracts = [typeof(ISystemAdminDbReadContext), typeof(ISystemAdminDbWriteContext)];
        var contractMethods = contracts.SelectMany(static type => type.GetMethods()).ToArray();
        var publicMethods = typeof(SystemAdminDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(SystemAdminDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(static method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, static method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(SystemAdminDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.SystemAdminDbContextExtensions");
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
        var context = File.ReadAllText(Path.Combine(folder, "SystemAdminDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "SystemAdminDbContextExtensions.cs"));

        Assert.DoesNotContain(source, line => line.Contains("partial class", StringComparison.Ordinal));
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(SystemAdminDbContext context)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Projection_query_results_are_owned_by_the_shared_domain()
    {
        var storage = typeof(SystemAdminDbContext).Assembly;
        var shared = typeof(DatabaseBackupProjectionCheckpointReadModel).Assembly;

        Assert.Equal("TomasAI.IFM.Domain.SystemAdmin.Shared", shared.GetName().Name);
        Assert.Equal(shared, typeof(DatabaseBackupProjectionRebuildResult).Assembly);
        Assert.NotEqual(storage, shared);
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.DatabaseBackupProjectionCheckpoint"));
        Assert.Null(storage.GetType("TomasAI.IFM.Application.Storage.DatabaseBackupProjectionRebuildResult"));
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
            && contract.GetParameters().Select(static parameter => parameter.ParameterType)
                .SequenceEqual(implementation.GetParameters().Select(static parameter => parameter.ParameterType));

    private static string GetFolder()
        => Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "SystemAdminDb");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
