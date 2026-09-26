using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<MarketDataDbContext>)
            .IsAssignableFrom(typeof(IMarketDataDbContext)));
        Assert.True(typeof(IMarketDataDbReadContext)
            .IsAssignableFrom(typeof(IMarketDataDbContext)));
        Assert.True(typeof(IMarketDataDbWriteContext)
            .IsAssignableFrom(typeof(IMarketDataDbContext)));
        Assert.True(typeof(IMarketDataDbContext)
            .IsAssignableFrom(typeof(MarketDataDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_market_data_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.MarketDataDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IMarketDataDbContext), property.PropertyType);
    }

    [Fact]
    public void Context_contract_owns_all_market_data_persistence_ports()
    {
        Type[] persistencePorts =
        [
            typeof(ICompositionPreparationStore),
            typeof(IOptionTradeEvidenceWriter),
            typeof(IOptionVolatilityRepository),
            typeof(IHistoricalObservationStore)
        ];

        Assert.All(persistencePorts, port =>
            Assert.True(port.IsAssignableFrom(typeof(IMarketDataDbContext)),
                $"{port.Name} must be owned by {nameof(IMarketDataDbContext)}."));
        Assert.All(persistencePorts, port =>
            Assert.True(port.IsAssignableFrom(typeof(MarketDataDbContext)),
                $"{port.Name} must be implemented by {nameof(MarketDataDbContext)}."));
    }

    [Fact]
    public void Legacy_standalone_market_data_stores_do_not_exist()
    {
        var assembly = typeof(MarketDataDbContext).Assembly;
        string[] retiredTypes =
        [
            "TomasAI.IFM.Application.Storage.MarketDataDb.CompositionPreparationStore",
            "TomasAI.IFM.Application.Storage.MarketDataDb.OptionTradeEvidenceStore",
            "TomasAI.IFM.Application.Storage.MarketDataDb.ScyllaOptionVolatilityRepository",
            "TomasAI.IFM.Application.Storage.MarketDataDb.HistoricalDataLoader.ScyllaHistoricalObservationStore"
        ];

        Assert.All(retiredTypes, typeName => Assert.Null(assembly.GetType(typeName)));
    }

    [Fact]
    public void Public_context_methods_are_read_or_write_contract_implementations()
    {
        var contractMethods = new[]
            {
                typeof(IMarketDataDbReadContext),
                typeof(IMarketDataDbWriteContext)
            }
            .SelectMany(type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(MarketDataDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_no_instance_helpers()
    {
        var declared = typeof(MarketDataDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName
                && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(MarketDataDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        Assert.NotEmpty(extensionMethods);
        Assert.All(extensionMethods, method =>
            Assert.True(method.IsDefined(typeof(ExtensionAttribute), inherit: false),
                $"{method.Name} must be declared as an extension method."));
    }

    [Fact]
    public void Extension_methods_use_csharp_14_blocks_and_have_xml_documentation()
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "TomasAI.IFM.Application.Storage",
            "MarketDataDb",
            "MarketDataDbContextExtensions.cs");
        var lines = File.ReadAllLines(sourcePath);
        var extensionBlocks = lines
            .Where(line => line.StartsWith("    extension", StringComparison.Ordinal))
            .ToArray();
        var declarations = lines
            .Select((text, index) => (Text: text, Index: index))
            .Where(line => line.Text.StartsWith("        internal ", StringComparison.Ordinal)
                && line.Text.Contains('('))
            .ToArray();

        Assert.NotEmpty(declarations);
        Assert.True(extensionBlocks.Length < declarations.Length);
        Assert.Equal(
            extensionBlocks.Length,
            extensionBlocks.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(lines, line =>
            line.StartsWith("    internal static ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("(this ", StringComparison.Ordinal));
        Assert.All(declarations, declaration =>
        {
            Assert.True(declaration.Index >= 3,
                $"Missing XML documentation for declaration: {declaration.Text.Trim()}");
            Assert.Equal("/// <summary>", lines[declaration.Index - 3].Trim());
            Assert.Equal("/// </summary>", lines[declaration.Index - 1].Trim());
        });
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_single_lines()
    {
        var repositoryRoot = FindRepositoryRoot();
        string[] sourceFiles =
        [
            Path.Combine(repositoryRoot, "TomasAI.IFM.Application.Storage", "MarketDataDb", "MarketDataDbContext.cs"),
            Path.Combine(repositoryRoot, "TomasAI.IFM.Application.Storage", "MarketDataDb", "MarketDataDbContextExtensions.cs")
        ];
        string[] sameLineBoundaries =
        [
            ").SetParameters(",
            ").ExecuteCommandAsync(",
            ").ExecuteSingleAsync(",
            ").ExecuteQueryAsync(",
            ").ExecuteStreamAsync(",
            ").ExecuteScalarAsync(",
            ").ExecutePageAsync(",
            ").ExecuteMapReduceAsync(",
            ").QueueCommand(",
            ").ConfigureAwait("
        ];
        var multilineInvocation = new Regex(
            @"\.(Use|SetParameters|Execute[A-Za-z]+|QueueCommand|ConfigureAwait)\([^\)]*$",
            RegexOptions.CultureInvariant);

        foreach (var sourceFile in sourceFiles)
        {
            var lines = File.ReadAllLines(sourceFile);
            Assert.DoesNotContain(lines, line =>
                sameLineBoundaries.Any(boundary =>
                    line.Contains(boundary, StringComparison.Ordinal)));
            Assert.DoesNotContain(lines, line => multilineInvocation.IsMatch(line));
        }
    }

    [Fact]
    public void Nonprimitive_storage_query_results_are_shared_domain_read_models()
    {
        Type[] resultTypes =
        [
            typeof(FmpQueryProjectionBackfillReadModel),
            typeof(FuturesTradeSignalRepairReadModel),
            typeof(MarketDataProjectionBackfillReadModel),
            typeof(MarketDataProjectionReadinessReadModel),
            typeof(MarketDataProjectionStateReadModel),
            typeof(MarketDataProjectionScopeStateReadModel),
            typeof(MarketDataProjectionScopeMutationReadModel),
            typeof(MarketDataProjectionMutationReadModel),
            typeof(VixFuturesContractIndexReadModel)
        ];

        Assert.All(resultTypes, type =>
        {
            Assert.Same(typeof(FmpQueryProjectionBackfillReadModel).Assembly, type.Assembly);
            Assert.Equal("TomasAI.IFM.Domain.MarketData.Shared.ViewModels", type.Namespace);
            Assert.EndsWith("ReadModel", type.Name, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Projection_support_is_owned_by_market_data_shared()
    {
        Type[] supportTypes =
        [
            typeof(MarketDataProjectionScopeGeneration),
            typeof(MarketDataProjectionScopeReadStamp),
            typeof(TickProjectionGuardFailureStage),
            typeof(ProjectionIdentity),
            typeof(ProjectionIdentityBuilder),
            typeof(MarketDataProjectionHash)
        ];

        Assert.All(supportTypes, type =>
        {
            Assert.Same(typeof(MarketDataProjectionScopeGeneration).Assembly, type.Assembly);
            Assert.Equal("TomasAI.IFM.Domain.MarketData.Shared", type.Namespace);
        });
        Assert.Null(typeof(MarketDataDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataProjectionHash"));
    }

    static bool SameSignature(MethodInfo contract, MethodInfo implementation)
        => contract.Name == implementation.Name
            && contract.ReturnType == implementation.ReturnType
            && contract.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(implementation.GetParameters().Select(parameter => parameter.ParameterType));

    static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repository root above {AppContext.BaseDirectory}.");
    }

    [Fact]
    public void Download_log_contracts_are_separated_by_behavior()
    {
        Assert.NotNull(typeof(IMarketDataDbReadContext)
            .GetMethod(nameof(IMarketDataDbReadContext.GetMarketDataDownloadLogAsync)));
        Assert.Null(typeof(IMarketDataDbReadContext)
            .GetMethod(nameof(IMarketDataDbWriteContext.InsertMarketDataDownloadLogAsync)));
        Assert.NotNull(typeof(IMarketDataDbWriteContext)
            .GetMethod(nameof(IMarketDataDbWriteContext.InsertMarketDataDownloadLogAsync),
            [typeof(MarketDataDownloadOutcome), typeof(Guid), typeof(string), typeof(CancellationToken)]));
        Assert.Null(typeof(IMarketDataDbWriteContext)
            .GetMethod(nameof(IMarketDataDbReadContext.GetMarketDataDownloadLogAsync)));
    }
}
