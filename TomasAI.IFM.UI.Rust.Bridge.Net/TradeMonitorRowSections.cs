namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>The fixed display sections that can reuse owned row documents between monitor refreshes.</summary>
public enum TradeMonitorSection { Established, Positions, Limits, Chart, PositionDetails, Observations }

/// <summary>Reuses unchanged display sections without conflating quote and history revisions.</summary>
public sealed class TradeMonitorRowSections : IDisposable
{
    sealed record Section(object? Source, long Revision, ContractEditorRow[] Rows);
    readonly Dictionary<TradeMonitorSection, Section> sections = new();
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
    public void Dispose() { disposed = true; sections.Clear(); }
}
