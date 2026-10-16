using System.Text.Json;
using System.Net.Http.Json;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Publishes read-only dashboard snapshots off the renderer thread. Does not start feeds or import reference data.</summary>
sealed class DashboardSession : IDisposable
{
    readonly CancellationTokenSource stopping = new();
    readonly Task worker;
    readonly MarketChartSession marketCharts = new();
    readonly SidebarSession sidebar = new();
    public byte[] SidebarPublication => sidebar.Publication;
    public bool SelectSidebar(uint action) => sidebar.Select(action);
    byte[] publication;
    public byte[] Publication => Volatile.Read(ref publication);
    readonly string title = $"Investment Fund Manager - v{typeof(FuturesEodDataUIViewModel).Assembly.GetName().Version} - DEV";
    public DashboardSession()
    {
        publication = BridgeWire.Serialize(new { title, status = "Loading dashboard...", outlook = new Dictionary<string,string>(), calendars = Array.Empty<string>() });
        worker = Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE") == "live" ? Task.Run(RunAsync) : Task.CompletedTask;
    }
    async Task RunAsync()
    {
        await using var scope = new RustClientServiceScope(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
        try
        {
            await scope.StartAsync(stopping.Token);
            var market = scope.Container.GetInstance<IMarketDataQueryApi>();
            var analytics = scope.Container.GetInstance<IMarketDataAnalyticsQueryApi>();
            var feed = scope.Container.GetInstance<IMarketDataFeedQueryApi>();
            using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            while (!stopping.IsCancellationRequested)
            {
                try
                {
                    var session = await market.GetMarketSessionAsync().WaitAsync(TimeSpan.FromSeconds(10), stopping.Token);
                    var contract = await market.GetOnTheRunFuturesContractAsync("ES").WaitAsync(TimeSpan.FromSeconds(10), stopping.Token);
                    if (!session.Success || session.Value is null || !contract.Success || contract.Value is null)
                        throw new InvalidOperationException(session.ErrorMessage ?? contract.ErrorMessage ?? "Dashboard session unavailable");
                    var runtime = await feed.GetRuntimeStatusAsync().WaitAsync(TimeSpan.FromSeconds(10), stopping.Token);
                    bool? feedRunning = runtime.Success && runtime.Value is { IsValid: true } ? runtime.Value.IsRunning : null;
                    string feedHealth = feedRunning == false ? "Inactive" : "Unknown";
                    string feedHealthReason = "Current pipeline health has not been verified.";
                    try
                    {
                        var health = scope.LivePipelineHealthEndpoint is { } healthEndpoint
                            ? await healthClient.GetFromJsonAsync<LivePipelineHealthSnapshot>(healthEndpoint, stopping.Token)
                            : null;
                        if (health is not null && health.ObservedUtc >= DateTime.UtcNow.AddMinutes(-2))
                        {
                            // Same classification as IFMAppViewModel's pipeline-health path.
                            feedHealth = health.Status switch
                            {
                                "Healthy" when feedRunning == true => session.Value.IsOffTrading ? "OffHoursActive" : "Healthy",
                                "Unhealthy" => "Critical",
                                _ => "Intermittent"
                            };
                            feedHealthReason = health.Checks.FirstOrDefault(c => c.Required && c.Status is "Degraded" or "Unhealthy" or "Unknown")?.Reason
                                ?? "All required stages verified.";
                        }
                    }
                    catch (Exception error) when (error is HttpRequestException or JsonException or TaskCanceledException && !stopping.IsCancellationRequested)
                    { feedHealthReason = "Pipeline health unavailable: " + error.Message; }
                    var date = session.Value.OperationalValueDate;
                    var outlook = await analytics.GetMarketOutlookSnapshotAsync(contract.Value.ContractId, date).WaitAsync(TimeSpan.FromSeconds(10), stopping.Token);
                    var values = new Dictionary<string,string>();
                    var cells = new Dictionary<string,MarketOutlookCell>();
                    var outlookStatus = "Market Outlook: no persisted snapshot";
                    if (outlook.Success && outlook.Value is { } snapshot)
                    {
                        var presentation = MarketOutlookPresentation.Create(snapshot, session.Value.IsLiveTrading, DateTimeOffset.UtcNow);
                        cells = presentation.Cells;
                        values = cells.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
                        outlookStatus = presentation.Status;
                    }
                    string[] calendars = [];
                    var status = outlook.Success ? $"{contract.Value.ContractId} | {date:yyyy-MM-dd} | {session.Value.State}" : $"Market Outlook unavailable: {outlook.ErrorMessage}";
                    Volatile.Write(ref publication, BridgeWire.Serialize(new { title, status, feedRunning, feedHealth, feedHealthReason, outlook = values, outlookCells = cells, outlookStatus, marketCharts = marketCharts.Charts, calendars }));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Volatile.Write(ref publication, BridgeWire.Serialize(new { title, status = "Dashboard unavailable: " + e.Message, outlook = new Dictionary<string,string>(), calendars = Array.Empty<string>() }));
                }
                await Task.Delay(TimeSpan.FromSeconds(5), stopping.Token);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (Exception e) { Volatile.Write(ref publication, BridgeWire.Serialize(new { title, status = "Dashboard unavailable: " + e.Message })); }
    }
    public void Dispose() { stopping.Cancel(); worker.GetAwaiter().GetResult(); stopping.Dispose(); marketCharts.Dispose(); sidebar.Dispose(); }
}
