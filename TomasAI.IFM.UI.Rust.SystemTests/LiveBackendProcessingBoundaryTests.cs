using System.Text.Json;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Opt-in read-only qualification against the user's running development backend. Never submits commands or starts feeds.</summary>
public sealed class LiveBackendProcessingBoundaryTests
{
    [LiveBackendBoundaryFact]
    public async Task Server_session_and_six_hour_saved_chart_are_available_without_enabling_live_feed()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var scope = new RustClientServiceScope(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
        await scope.StartAsync(deadline.Token);
        var id = await ResolveEstablishedIdentityAsync(scope, deadline.Token);
        var day = await scope.GetValueDateAsync(deadline.Token);
        var session = await scope.Services.MarketDataQueries.QueryMarketSessionAsync(deadline.Token);
        Assert.True(session.IsValid); Assert.Equal(day, session.OperationalValueDate);
        var position = StrategyPositionId.Create(id, TradeStrategyKind.IronCondor);
        var chart = await scope.Services.StrategyTradePlanQueries.GetIronCondorChartAsync(position, day, deadline.Token);
        Assert.Equal(TimeSpan.FromHours(6), chart.EndUtc - chart.StartUtc);
        Assert.NotEmpty(chart.Points);
        Assert.All(chart.Points, point => Assert.InRange(point.AtUtc, chart.StartUtc, chart.EndUtc));
        Assert.Equal(chart.Points.OrderBy(point => point.AtUtc), chart.Points);
        // Position history stores one stable daily row; its timestamp is not a six-hour chart sample.
        var history = await scope.Services.StrategyPositions.GetHistoryAsync(position, TradeStrategyKind.IronCondor,
            session.AsOfUtc.AddDays(-30), session.AsOfUtc, deadline.Token);
        Assert.NotEmpty(history);
        Assert.All(history, row => { Assert.Equal(position, row.Id); Assert.NotNull(row.HistoryValuation); });
        var directory = Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "backend-session-history-results.json"), JsonSerializer.Serialize(new {
            valueDate = day, session, tradeId = id.Format(), chart.StartUtc, chart.EndUtc, pointCount = chart.Points.Length,
            historyCount = history.Length, historyDates = history.Select(row => row.ValueDate).Distinct(),
            note = "Read-only API/current UI service checks. No feed acquisition, submission, posting, or server restart."
        }, new JsonSerializerOptions { WriteIndented = true }), deadline.Token);
    }

    [LiveBackendScreenFact]
    public void Inspect_running_trade_screen_without_clicking_or_changing_live_feed()
    {
        var pid=int.Parse(Environment.GetEnvironmentVariable("IFM_BOUNDARY_UI_PID")!);
        var handle=NativeWindows.Find(pid);
        Assert.NotEqual(IntPtr.Zero,handle);
        using var automation=new FlaUI.UIA3.UIA3Automation { TransactionTimeout=TimeSpan.FromSeconds(5),ConnectionTimeout=TimeSpan.FromSeconds(5) };
        var window=automation.FromHandle(handle);
        var controls=window.FindAllDescendants();
        Assert.NotEmpty(controls);
        var directory=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"running-rust-screen.json"),JsonSerializer.Serialize(controls.Select(c=>new {
            name=c.Properties.Name.ValueOrDefault,type=c.ControlType.ToString(),bounds=c.BoundingRectangle.ToString()
        }),new JsonSerializerOptions { WriteIndented=true }));
        window.Capture().Save(Path.Combine(directory,"running-rust-screen.png"));
    }

    [LiveBackendBoundaryFact]
    public async Task Metrics_saved_history_and_preparation_are_served_by_running_backend()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var scope = new RustClientServiceScope(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
        await scope.StartAsync(deadline.Token);
        var id = await ResolveEstablishedIdentityAsync(scope, deadline.Token);
        var day = await scope.GetValueDateAsync(deadline.Token);
        using var metrics = new FundMetricsViewModel(scope.Services.PortfolioFinancial);
        await metrics.LoadAsync(id.PortfolioId,id.FundId,day.AddDays(-30),day).WaitAsync(deadline.Token);
        Assert.NotNull(metrics.Report);
        Assert.Contains("LegacyLedgerPostings/v1",metrics.Report.CalculationBasis);
        var tradeRead = await scope.Services.EstablishedTrades.GetAsync(id,TradeStrategyKind.IronCondor,deadline.Token);
        Assert.True(tradeRead.Success,tradeRead.ErrorMessage); Assert.NotNull(tradeRead.Value);
        var trade = tradeRead.Value;
        Assert.NotNull(trade.OpeningHistoryValuation);
        var realtime = await scope.Services.EstablishedTrades.GetRealtimeAsync(id,day,deadline.Token);
        Assert.True(realtime.Success,realtime.ErrorMessage);
        var quantity = trade.Legs.Select(l=>Math.Abs(l.SignedQuantity)).Min();
        var component = new TradeOrderComponentDefinition { ComponentId=Guid.NewGuid(),StrategyKind=TradeStrategyKind.IronCondor,
            Legs=trade.Legs,SignedNetDebitLimit=trade.OpeningValue };
        var draft = new PortfolioOrderCandidate { CompositionId=Guid.NewGuid(),WorkflowId=Guid.NewGuid(),
            BrokerEnvironment=PortfolioBrokerEnvironment.Emulator,BrokerAccountAlias=scope.TradeExecution.AccountAlias,
            SetupTrade=new() { FundId=id.FundId,OrderId=id.OrderId,TradeId=id.TradeId },Components=[component.ToPortfolioComponent()] };
        var underlying = Environment.GetEnvironmentVariable("IFM_BOUNDARY_UNDERLYING")!;
        var prepared = await scope.Services.TradeQueries.PrepareTradeOrderAsync(new(id.PortfolioId,underlying,quantity,draft),deadline.Token);
        Assert.True(prepared.Success,prepared.ErrorMessage); Assert.NotNull(prepared.Value);
        Assert.Equal(day,prepared.Value.Candidate.ValueDate);
        var resolvedLegs=prepared.Value.Candidate.Components.Single().ToTradeComponent().Legs;
        Assert.Equal(trade.OpeningValue*quantity*resolvedLegs[0].CashMultiplier,prepared.Value.CashAmount);
        var directory=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"backend-processing-live-results.json"),JsonSerializer.Serialize(new {
            valueDate=day,tradeId=id.Format(),metrics=metrics.Report,message=metrics.Message,
            opening=trade.OpeningHistoryValuation,openingSnapshotAvailable=trade.OpeningPositionSnapshot is not null,
            realtimeAvailable=realtime.Value is not null,realtimeRevision=realtime.Value?.SnapshotRevision,realtimeSnapshot=realtime.Value,
            preparation=prepared.Value, note="Read-only preparation using saved trade opening price; no order submitted; not a current executable quote."
        },new JsonSerializerOptions { WriteIndented=true }),deadline.Token);
    }

    static async Task<TradeEntityId> ResolveEstablishedIdentityAsync(RustClientServiceScope scope, CancellationToken token)
    {
        // Setup and established execution identities are separate domains; never infer one from the other.
        if (Environment.GetEnvironmentVariable("IFM_BOUNDARY_ESTABLISHED_TRADE_ID") is { Length: > 0 } established)
            return TradeEntityId.Parse(established);
        var setup = TradeEntityId.Parse(Environment.GetEnvironmentVariable("IFM_BOUNDARY_TRADE_ID")!);
        var read = await scope.Services.PortfolioQueries.GetTradeAsync(setup.TradeId, token);
        Assert.True(read.Success, read.ErrorMessage); Assert.NotNull(read.Value);
        Assert.Equal(setup.PortfolioId, read.Value.PortfolioId); Assert.Equal(setup.FundId, read.Value.FundId);
        Assert.True(read.Value.ExecutionOrderId > 0 && read.Value.ExecutionTradeId > 0);
        return new(setup.PortfolioId, setup.FundId, read.Value.ExecutionOrderId, read.Value.ExecutionTradeId);
    }
}

public sealed class LiveBackendScreenFactAttribute : FactAttribute
{
    public LiveBackendScreenFactAttribute()
    {
        if(Environment.GetEnvironmentVariable("IFM_BOUNDARY_UI_PID") is null)
            Skip="Requires an explicitly selected running Rust UI PID; read-only desktop inspection.";
    }
}

public sealed class LiveBackendBoundaryFactAttribute : FactAttribute
{
    public LiveBackendBoundaryFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_BOUNDARY_LIVE")!="1")
            Skip="Requires explicit running-backend opt-in, a setup IFM_BOUNDARY_TRADE_ID or IFM_BOUNDARY_ESTABLISHED_TRADE_ID, and IFM_BOUNDARY_UNDERLYING.";
    }
}
