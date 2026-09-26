using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

public sealed class TradePlanDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<TradePlanDbContext>)
            .IsAssignableFrom(typeof(ITradePlanDbContext)));
        Assert.True(typeof(ITradePlanDbReadContext)
            .IsAssignableFrom(typeof(ITradePlanDbContext)));
        Assert.True(typeof(ITradePlanDbWriteContext)
            .IsAssignableFrom(typeof(ITradePlanDbContext)));
        Assert.True(typeof(ITradePlanDbContext)
            .IsAssignableFrom(typeof(TradePlanDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_trade_plan_context()
    {
        var property = typeof(IDbContextFactory)
            .GetProperty(nameof(IDbContextFactory.TradePlanDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(ITradePlanDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_contract_implementations()
    {
        Type[] contracts =
        [
            typeof(ITradePlanDbReadContext),
            typeof(ITradePlanDbWriteContext)
        ];
        var contractMethods = contracts
            .SelectMany(static type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(TradePlanDbContext)
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
        var declared = typeof(TradePlanDbContext)
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

        var extensions = typeof(TradePlanDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.TradePlanDb.TradePlanDbContextExtensions");
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
            Path.Combine(folder, "TradePlanDbContext.cs"));
        var extensions = File.ReadAllText(
            Path.Combine(folder, "TradePlanDbContextExtensions.cs"));

        Assert.DoesNotContain(
            source,
            line => line.Contains(
                "partial class TradePlanDbContext",
                StringComparison.Ordinal));
        Assert.DoesNotContain("throw new Argument", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains(
            "extension(TradePlanDbContext context)",
            extensions,
            StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_results_are_owned_by_the_trade_shared_domain()
    {
        var storage = typeof(TradePlanDbContext).Assembly;
        var shared = typeof(StrategyTradePlanSnapshot).Assembly;

        Assert.Equal("TomasAI.IFM.Domain.Trade.Shared", shared.GetName().Name);
        Assert.Equal(shared, typeof(ExitPositionWorkflowProjection).Assembly);
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
            "TradePlanDb");

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
