using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed record MarketOutlookCell(string Value, string Foreground, string Background);
/// <summary>Shares the WinForms composite snapshot presentation without accessing controls or changing calculations.</summary>
public sealed record MarketOutlookPresentation(Dictionary<string, MarketOutlookCell> Cells, string Status)
{
    public static MarketOutlookPresentation Create(MarketOutlookReadModel snapshot, bool live, DateTimeOffset now)
    {
        var cells = new Dictionary<string, MarketOutlookCell>();
        Add(new FuturesEodDataUIViewModel(snapshot));
        Add(new FuturesTradeSignalUIViewModel(snapshot));
        // The close price textbox is yellow on black in the WinForms designer.
        if (cells.TryGetValue("closePrice", out var close)) cells["closePrice"] = close with { Foreground = "Caution" };
        var updated = new DateTimeOffset(DateTime.SpecifyKind(snapshot.UpdatedAtUtc, DateTimeKind.Utc));
        var freshness = live && now - updated > TimeSpan.FromMinutes(5) ? "STALE" : live ? "live" : "current";
        var completeness = string.IsNullOrWhiteSpace(snapshot.MissingInputs) ? "complete" : $"warming/degraded: {snapshot.MissingInputs}";
        return new(cells, $"Market Outlook {snapshot.RefreshTrigger} | {snapshot.ValueDate:yyyy-MM-dd} | {freshness} | {completeness}");

        void Add(object model)
        {
            var properties = model.GetType().GetProperties();
            foreach (var property in properties.Where(p => p.PropertyType == typeof(string)))
            {
                var foreground = properties.FirstOrDefault(p => p.Name == property.Name + "ForeColor")?.GetValue(model) as PresentationColorRole?;
                var background = properties.FirstOrDefault(p => p.Name == property.Name + "BackColor")?.GetValue(model) as PresentationColorRole?;
                cells[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = new((string?)property.GetValue(model) ?? "N/A", foreground?.ToString() ?? "LightText", background?.ToString() ?? "DarkSurface");
            }
        }
    }
}
