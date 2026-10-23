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
        var setupId = TradeEntityId.Parse(Environment.GetEnvironmentVariable("IFM_BOUNDARY_TRADE_ID")!);
        var setupRead = await scope.Services.PortfolioQueries.GetTradeAsync(setupId.TradeId,deadline.Token);
        Assert.True(setupRead.Success,setupRead.ErrorMessage); Assert.NotNull(setupRead.Value);
        Assert.Equal(setupId.PortfolioId,setupRead.Value.PortfolioId);
        Assert.Equal(setupId.FundId,setupRead.Value.FundId);
        Assert.True(setupRead.Value.ExecutionOrderId>0 && setupRead.Value.ExecutionTradeId>0);
        var id = new TradeEntityId(setupId.PortfolioId,setupId.FundId,setupRead.Value.ExecutionOrderId,setupRead.Value.ExecutionTradeId);
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
            Skip="Requires explicit running-backend opt-in and IFM_BOUNDARY_TRADE_ID / IFM_BOUNDARY_UNDERLYING.";
    }
}
