namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Architecture;

public sealed class ApplicationLifecycleOwnershipTests
{
    readonly string root = FindRepositoryRoot();

    [Fact]
    public void UI_initialization_has_no_automatic_backend_lifecycle_mutation()
    {
        var source = File.ReadAllText(Path.Combine(
            root,
            "TomasAI.IFM.UI.Net.ViewModels",
            "App",
            "IFMAppViewModel.cs"));
        var startup = Slice(source, "Task StartApplicationCoreAsync", "async Task MonitorMarketSessionAsync");
        var stop = Slice(source, "async Task StopCoreAsync", "async Task ShutdownCorePresentationAsync");
        var transition = Slice(source, "async Task ApplyMarketSessionTransitionAsync", "internal void ApplyMarketSessionSnapshot");

        Assert.DoesNotContain("ImportReferenceDataAtStartupAsync", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureHistoricalAnalyticsWarmupAsync", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableTradeLiveFeed", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("StartFuturesIntradaySignalServices", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableMarketDataFeedResetListener", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("DisableTradeLiveFeed", stop, StringComparison.Ordinal);
        Assert.DoesNotContain("StopFuturesIntradaySignalServices", stop, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableTradeLiveFeed", transition, StringComparison.Ordinal);
        Assert.DoesNotContain("DisableTradeLiveFeed", transition, StringComparison.Ordinal);
    }

    [Fact]
    public void API_registers_actor_dispatch_and_not_the_legacy_rollover_host()
    {
        var source = File.ReadAllText(Path.Combine(
            root,
            "TomasAI.IFM.Application.Api.Server",
            "Startup.cs"));
        Assert.Contains("AddHostedService<ApplicationStartupCommandDispatcher>()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<FuturesContractRolloverStartupService>()", source, StringComparison.Ordinal);
        Assert.Contains("tags: [\"bootstrap\", \"launch\", \"ready\"]", source, StringComparison.Ordinal);
        Assert.Contains("AddCheck<ActorRuntimeHealthCheck>(\"actor_runtime\", tags: [\"actor\", \"bootstrap\", \"launch\", \"ready\"])", source, StringComparison.Ordinal);
        Assert.Contains("/health/bootstrap", source, StringComparison.Ordinal);
        Assert.Contains("/health/actors", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Market_data_health_is_operational_readiness_not_ui_launch_readiness()
    {
        var source = File.ReadAllText(Path.Combine(
            root,
            "TomasAI.IFM.Application.Api.Server",
            "Startup.cs"));

        Assert.Contains(
            """AddCheck<MarketDataRuntimeHealthCheck>("market_data_runtime", tags: ["application", "ready"])""",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            """AddCheck<MarketDataRuntimeHealthCheck>("market_data_runtime", tags: ["application", "launch", "ready"])""",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Actor_startup_is_concurrent_and_actor_health_gates_application_readiness()
    {
        var actorStartup = File.ReadAllText(Path.Combine(
            root,
            "TomasAI.IFM.Shared",
            "EventModelActor",
            "ActorRuntimeStartup.cs"));
        var apiStartup = File.ReadAllText(Path.Combine(
            root,
            "TomasAI.IFM.Application.Api.Server",
            "Startup.cs"));

        Assert.Contains("Task.WhenAll(actors.Select((actor, index)", actorStartup, StringComparison.Ordinal);
        Assert.Contains(
            "AddCheck<ActorRuntimeHealthCheck>(\"actor_runtime\", tags: [\"actor\", \"bootstrap\", \"launch\", \"ready\"])",
            apiStartup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Lifecycle_paths_report_to_console_without_presentation_errors()
    {
        var source = File.ReadAllText(Path.Combine(
            root,
            "TomasAI.IFM.UI.Net.ViewModels",
            "App",
            "IFMAppViewModel.cs"));
        var applicationListener = Slice(source, "Task StartApplicationEventsListener", "void StartupOpenTrades");
        var feedListener = Slice(source, "Task StartMarketDataFeedStatusListener", "Task StopMarketDataFeedStatusListener");
        Assert.DoesNotContain("PublishError", applicationListener, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishError", feedListener, StringComparison.Ordinal);
        Assert.Contains("WriteStatusConsoleAsync", applicationListener, StringComparison.Ordinal);
        Assert.Contains("WriteStatusConsoleAsync", feedListener, StringComparison.Ordinal);
    }

    static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing source marker {start}.");
        Assert.True(endIndex > startIndex, $"Missing source marker {end}.");
        return source[startIndex..endIndex];
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Unable to locate IFM repository root.");
    }
}
