using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class MarketOutlookPresentationTests
{
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
}
