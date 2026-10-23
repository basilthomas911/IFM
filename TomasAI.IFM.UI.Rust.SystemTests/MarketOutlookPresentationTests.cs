using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class MarketOutlookPresentationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Daily_baseline_availability_is_visible_in_dashboard_status(bool available)
    {
        var snapshot = new MarketOutlookReadModel { ContractId = "ES", ValueDate = new(2026, 10, 23), UpdatedAtUtc = DateTime.UtcNow,
            FuturesEmaSignal = available ? new() { IsWarm = true, BaselineValueDate = new(2026, 10, 22) } : null };
        var status = MarketOutlookPresentation.Create(snapshot, true, DateTimeOffset.UtcNow).Status;
        Assert.Contains(available ? "EMA/BB baseline 2026-10-22" : "EMA/BB baseline unavailable", status);
    }
    [Fact]
    public void Composite_fields_and_color_roles_match_the_WinForms_view_models()
    {
        var snapshot = new MarketOutlookReadModel { ContractId="ES", ValueDate=new(2026,10,13), UpdatedAtUtc=DateTime.UtcNow };
        var presentation = MarketOutlookPresentation.Create(snapshot,true,DateTimeOffset.UtcNow);
        var eod = new FuturesEodDataUIViewModel(snapshot);
        var trade = new FuturesTradeSignalUIViewModel(snapshot);
        Assert.Equal(eod.MarketDirection,presentation.Cells["marketDirection"].Value);
        Assert.Equal(eod.MarketDirectionBackColor.ToString(),presentation.Cells["marketDirection"].Background);
        Assert.Equal(eod.Adx,presentation.Cells["adx"].Value);
        Assert.Equal(trade.RSI,presentation.Cells["rsi"].Value);
        Assert.Equal(trade.RSIBackColor.ToString(),presentation.Cells["rsi"].Background);
        Assert.Equal(trade.FiftyDMA,presentation.Cells["fiftyDMA"].Value);
        Assert.Equal(trade.TdiDirection,presentation.Cells["tdiDirection"].Value);
        Assert.Equal(trade.Trend,presentation.Cells["trend"].Value);
        Assert.Equal("Caution",presentation.Cells["closePrice"].Foreground);
        Assert.Contains("live | complete",presentation.Status);
        var stale = MarketOutlookPresentation.Create(snapshot with { UpdatedAtUtc=DateTime.UtcNow.AddMinutes(-10), MissingInputs="TDI" },true,DateTimeOffset.UtcNow);
        Assert.Contains("STALE | warming/degraded: TDI",stale.Status);
    }
    [Fact]
    public void Dashboard_wire_preserves_nested_messagepack_contract_keys_and_colors()
    {
        var snapshot = new MarketOutlookReadModel { ContractId="ES", ValueDate=new(2026,10,16), UpdatedAtUtc=DateTime.UtcNow };
        var presentation=MarketOutlookPresentation.Create(snapshot,true,DateTimeOffset.UtcNow);
        var dashboard=new { outlookCells=presentation.Cells, marketCharts=new Dictionary<string,MarketChartSnapshot>{["ES"]=new("live",1,2,3,4,[new("ES","yellow",[[1,3],[2,4]])])} };
        var bytes=BridgeWire.Serialize(dashboard);
        using var document=System.Text.Json.JsonDocument.Parse(MessagePack.MessagePackSerializer.ConvertToJson(bytes));
        var cell=document.RootElement.GetProperty("outlookCells").GetProperty("marketDirection");
        Assert.Equal(presentation.Cells["marketDirection"].Value,cell.GetProperty("value").GetString());
        Assert.Equal(presentation.Cells["marketDirection"].Foreground,cell.GetProperty("foreground").GetString());
        Assert.Equal(presentation.Cells["marketDirection"].Background,cell.GetProperty("background").GetString());
        Assert.Equal("DarkSurface",document.RootElement.GetProperty("outlookCells").GetProperty("closePrice").GetProperty("background").GetString());
        var graph=document.RootElement.GetProperty("marketCharts").GetProperty("ES");
        Assert.Equal("live",graph.GetProperty("status").GetString());
        Assert.Equal(2,graph.GetProperty("series")[0].GetProperty("points").GetArrayLength());
        var fixture=Environment.GetEnvironmentVariable("IFM_TRANSFER_FIXTURES");
        if(fixture is not null){Directory.CreateDirectory(fixture);File.WriteAllBytes(Path.Combine(fixture,"dashboard.msgpack"),bytes);}
    }
}
