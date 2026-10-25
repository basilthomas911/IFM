namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>The fixed display sections that can reuse owned row documents between monitor refreshes.</summary>
public enum TradeMonitorSection { Established, Positions, Limits, Chart, PositionDetails, Observations }

/// <summary>Reuses unchanged display sections without conflating quote and history revisions.</summary>
public sealed class TradeMonitorRowSections : IDisposable
{
    sealed record Section(object? Source, long Revision, ContractEditorRow[] Rows);
    readonly Dictionary<TradeMonitorSection, Section> sections = new();
    TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeHistoryReadModel[]? positionSource;
    ContractEditorRow[] positionRows = [];
    string? positionCulture;
    bool disposed;
    public ContractEditorRow[] Rows<T>(TradeMonitorSection section, object? source, long revision, T state,
        Func<T, ContractEditorRow[]> encode)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sections.TryGetValue(section, out var old) && ReferenceEquals(old.Source, source) && old.Revision == revision)
            return old.Rows;
        var rows = encode(state);
        sections[section] = new(source, revision, rows);
        return rows;
    }
    /// <summary>Encodes only changed position rows; immutable historical row payloads are reused.</summary>
    public ContractEditorRow[] PositionRows(TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeHistoryReadModel[] history)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var culture = System.Globalization.CultureInfo.CurrentCulture.Name;
        if (ReferenceEquals(positionSource, history) && positionCulture == culture) return positionRows;
        var rows = new ContractEditorRow[history.Length];
        for (var i = 0; i < history.Length; i++)
            rows[i] = positionCulture == culture && positionSource is { } prior && i < prior.Length
                && ReferenceEquals(prior[i], history[i]) ? positionRows[i]
                : new("position:" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), new() { ["kind"] = "position" })
                { Data = BridgeDocument.Encode(history[i], camelCase: true, enumNames: true) };
        positionSource = history;
        positionRows = rows;
        positionCulture = culture;
        return rows;
    }
    public void Dispose() { disposed = true; sections.Clear(); positionSource = null; positionRows = []; }
}
