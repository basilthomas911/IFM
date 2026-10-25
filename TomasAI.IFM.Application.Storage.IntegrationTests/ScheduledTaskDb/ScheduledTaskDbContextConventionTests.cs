using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ScheduledTaskDb;

public sealed class ScheduledTaskDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<ScheduledTaskDbContext>).IsAssignableFrom(typeof(IScheduledTaskDbContext)));
        Assert.True(typeof(IScheduledTaskDbReadContext).IsAssignableFrom(typeof(IScheduledTaskDbContext)));
        Assert.True(typeof(IScheduledTaskDbWriteContext).IsAssignableFrom(typeof(IScheduledTaskDbContext)));
        Assert.True(typeof(IScheduledTaskDbContext).IsAssignableFrom(typeof(ScheduledTaskDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_scheduled_task_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.ScheduledTaskDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IScheduledTaskDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_read_or_write_contract_implementations()
    {
        var contractMethods = new[] { typeof(IScheduledTaskDbReadContext), typeof(IScheduledTaskDbWriteContext) }
            .SelectMany(type => type.GetInterfaces().Append(type))
            .SelectMany(type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(ScheduledTaskDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => contract.ToString() == implementation.ToString()));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(ScheduledTaskDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(ScheduledTaskDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskDbContextExtensions");
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
        var folder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "ScheduledTaskDb");
        var source = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadAllLines)
            .ToArray();
        var context = File.ReadAllText(Path.Combine(folder, "ScheduledTaskDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "ScheduledTaskDbContextExtensions.cs"));

        Assert.DoesNotContain(source, line => line.Contains("partial class", StringComparison.Ordinal));
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(ScheduledTaskDbContext context)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "ScheduledTaskDb");
        string[] files =
        [
            Path.Combine(folder, "ScheduledTaskDbContext.cs"),
            Path.Combine(folder, "ScheduledTaskDbContextExtensions.cs")
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

    [Fact]
    public void Sql_schema_bindings_and_artifacts_have_one_owner()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "ScheduledTaskDb");
        Assert.False(File.Exists(Path.Combine(folder, "PostgresScheduledTaskStore.cs")));
        Assert.False(File.Exists(Path.Combine(folder, "ScheduledTaskReadStore.cs")));
        Assert.False(File.Exists(Path.Combine(folder, "ScheduledTaskOutputReader.cs")));
        foreach (var path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("Scylla", source, StringComparison.Ordinal);
            if (source.Contains(": IBindValue", StringComparison.Ordinal)) Assert.Equal("ScheduledTaskDbParameters.cs", Path.GetFileName(path));
            if (source.Contains("CREATE TABLE", StringComparison.Ordinal)) Assert.Equal("ScheduledTaskSchemaSql.cs", Path.GetFileName(path));
        }
        Assert.True(File.Exists(Path.Combine(folder, "Schema", "ScheduledTaskSchemaDb.cs")));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
