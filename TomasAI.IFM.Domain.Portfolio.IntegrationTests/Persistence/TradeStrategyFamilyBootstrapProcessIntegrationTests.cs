using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection(PortfolioPersistenceCollection.Name)]
public sealed class TradeStrategyFamilyBootstrapProcessIntegrationTests
{
    const string ReferenceConnection = "Contact Points=localhost;Port=9042;Default Keyspace=reference_test_db";
    const string SequenceConnection = "Host=localhost;Port=5432;Database=sequence-id-test-db";

    [Fact]
    [Trait("Category", "Portfolio")]
    [Trait("Gate", "PF-22")]
    public async Task Simultaneous_processes_seed_exactly_one_row_per_stable_family_key()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var settings = new DbConnectionSettings();
        settings.Add(ReferenceDbContext.ReferenceDbConnection, ReferenceConnection, "System.Data.ScyllaDb");
        settings.Add(SequenceIdDbContext.SequenceIdDbConnection, SequenceConnection, "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var schema = new ReferenceSchemaDb(settings, logger);
        await schema.RecreateAsync(["trade_strategy_family_v2"], timeout.Token);
        await new SequenceIdSchemaDb(settings, logger).CreateAllAsync();

        var processRuns = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => RunBootstrapProcessAsync(timeout.Token)));
        processRuns.Should().OnlyContain(x => x.ExitCode == 0, string.Join(Environment.NewLine, processRuns.Select(x => x.Output)));

        // A later independent process is the restart/idempotency proof.
        var restart = await RunBootstrapProcessAsync(timeout.Token);
        restart.ExitCode.Should().Be(0, restart.Output);

        var rows = await ReadFamiliesAsync(settings, logger, timeout.Token);
        rows.Should().HaveCount(3);
        rows.Select(x => (x.SystemKey, x.DefinitionVersion)).Should().OnlyHaveUniqueItems();
        rows.Select(x => x.TradeStrategyFamilyId).Should().OnlyHaveUniqueItems().And.OnlyContain(x => x > 0);
        rows.Select(x => x.SystemKey).Should().Equal("FUTURES", "VERTICAL_SPREAD", "IRON_CONDOR");
    }

    static async Task<(int ExitCode, string Output)> RunBootstrapProcessAsync(CancellationToken cancellationToken)
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Parent!.Parent!.Parent!.FullName;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var serverDirectory = Path.Combine(repositoryRoot, "TomasAI.IFM.Application.Api.Server", "bin", configuration, "net10.0");
        var serverAssembly = Path.Combine(serverDirectory, "TomasAI.IFM.Application.Api.Server.dll");
        File.Exists(serverAssembly).Should().BeTrue($"the bootstrap host must be built at {serverAssembly}");

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = serverDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(serverAssembly);
        start.ArgumentList.Add("--bootstrap-trade-strategy-families-only");
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start bootstrap process.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode, $"{await output}{await error}");
    }

    static async Task<IReadOnlyList<TradeStrategyFamilyReadModel>> ReadFamiliesAsync(
        DbConnectionSettings settings,
        ILogger<DbProvider> logger,
        CancellationToken cancellationToken)
    {
        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var context = new ReferenceDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), logger);
        repositories.Add(typeof(IObjectRepository<ReferenceDbContext>), context);
        return await context.GetTradeStrategyFamiliesAsync(cancellationToken);
    }
}
