using FluentAssertions;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

[Trait("Category", "G2Infrastructure")]
public sealed class G2InfrastructureTests
{
    [Fact]
    public void Mutation_safety_policy_accepts_only_test_databases_and_contained_backup_output()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ifm-g2-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var serverConfiguration = Path.Combine(root, "appsettings.Development.json");
            var api = Path.Combine(root, "api.exe");
            var desktop = Path.Combine(root, "ui.exe");
            File.WriteAllText(serverConfiguration, "{}");
            File.WriteAllText(api, string.Empty);
            File.WriteAllText(desktop, string.Empty);
            var results = Path.Combine(root, "results");
            var configuration = NewConfiguration(
                root,
                results,
                serverConfiguration,
                api,
                desktop,
                [new G2DatabaseIdentity("MarketData", "market_data_test_db")]);

            configuration.Validate().Should().BeEmpty();

            var unsafeConfiguration = NewConfiguration(
                root,
                results,
                serverConfiguration,
                api,
                desktop,
                [new G2DatabaseIdentity("MarketData", "market_data_prod_db")],
                Path.Combine(root, "operator-backups"));
            unsafeConfiguration.Validate().Should().Contain(error =>
                error.Contains("non-test identity", StringComparison.Ordinal));
            unsafeConfiguration.Validate().Should().Contain(error =>
                error.Contains("contained", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Command_listener_catalog_has_unique_exact_routes_and_complete_fail_coverage()
    {
        var registrations = G2CommandEventObserver.Registrations;
        string[] requiredFamilies =
        [
            "MarketDataFeed", "FuturesContract", "FuturesOptionContract", "YieldCurve",
            "EconomicCalendar", "LookupType", "Fund", "FundTransaction", "FundOrder",
            "EndOfDay", "DatabaseBackup"
        ];

        registrations.Select(item => $"{item.Actor}|{item.Verb}").Should().OnlyHaveUniqueItems();
        foreach (var family in requiredFamilies)
        {
            registrations.Should().Contain(item => item.Family == family && item.Success == true);
            registrations.Should().Contain(item => item.Family == family && item.Success == false);
        }
        registrations.Should().Contain(item =>
            item.Family == "MarketDataFeed"
            && item.EventType == "MarketDataFeedStartedEvent"
            && item.Success == null);
        registrations.Should().Contain(item =>
            item.Family == "MarketDataFeed"
            && item.EventType == "MarketDataFeedStoppedEvent"
            && item.Success == null);
        foreach (var family in new[] { "FuturesContract", "FuturesOptionContract" })
        {
            registrations.Count(item => item.Family == family && item.Success is null)
                .Should().Be(3, $"{family} must expose add/change/remove source-event routes");
        }
        registrations.Count(item => item.Family == "YieldCurve" && item.Success is null)
            .Should().Be(4, "yield-curve must expose add/change/remove/import source-event routes");
        registrations.Count(item => item.Family == "EconomicCalendar" && item.Success is null)
            .Should().Be(4, "economic-calendar must expose add/change/remove/import source-event routes");
        registrations.Count(item => item.Family == "LookupType" && item.Success is null)
            .Should().Be(3, "lookup maintenance must expose add/change/remove source-event routes");
        registrations.Should().OnlyContain(item =>
            !string.IsNullOrWhiteSpace(item.Actor)
            && !string.IsNullOrWhiteSpace(item.Verb)
            && !string.IsNullOrWhiteSpace(item.EventType));
    }

    static G2Configuration NewConfiguration(
        string repositoryRoot,
        string resultsRoot,
        string serverConfiguration,
        string api,
        string desktop,
        G2DatabaseIdentity[] databases,
        string? backupRoot = null)
        => new()
        {
            Process = new G0Configuration
            {
                RunId = "unit-test",
                EnvironmentName = "Development",
                RepositoryRoot = repositoryRoot,
                ApiExecutable = api,
                DesktopExecutable = desktop,
                ResultsRoot = resultsRoot,
                ApiReadyUri = new Uri("http://localhost:22543/health/ready"),
                NatsUri = new Uri("nats://localhost:4222"),
                PostgreSql = new G0Endpoint("PostgreSQL", "localhost", 5432),
                ScyllaDb = new G0Endpoint("ScyllaDB", "localhost", 9042),
                Redis = new G0Endpoint("Redis", "localhost", 6379),
                FmpAdapter = "Production",
                FmpCredentialPresent = true,
                DeterministicAdapterApproved = false,
                ReadinessTimeout = TimeSpan.FromSeconds(1),
                StartupTimeout = TimeSpan.FromSeconds(1),
                ShutdownTimeout = TimeSpan.FromSeconds(1),
                AuditTimeout = TimeSpan.FromSeconds(1)
            },
            RunPrefix = "G2-UNITTEST",
            ImportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            YieldCurveManualDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2).AddDays(1)),
            EconomicCalendarManualDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2).AddDays(2)),
            ImportCountryCodes = ["US"],
            FundFixtureName = "IFM G2 Automation Fund",
            SecuritiesSymbol = "ES",
            SecuritiesMaturityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            SecuritiesOptionStrike = 4500,
            BackupDestinationRoot = backupRoot ?? Path.Combine(resultsRoot, "backups", "unit-test"),
            ServerConfigurationPath = serverConfiguration,
            DatabaseIdentities = databases
        };
}
