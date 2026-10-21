using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Identifies a history display source independently from index-based Rust row identifiers.</summary>
public readonly record struct TradePlanHistoryScope(StrategyPositionId Position, DateOnly ValueDate, bool Live);

/// <summary>Owns bounded encoded history rows. Cache eviction never removes source plans or visible rows.</summary>
public sealed class TradePlanRowCache : IDisposable
{
    readonly record struct Identity(TradePlanHistoryScope Scope, StrategyPositionId Position, DateOnly ValueDate,
        long RouteGeneration, long PlanRevision, string Culture, int DisplaySchema = 1);
    sealed record Entry(Identity Id, WeakReference<StrategyTradePlanSnapshot> Source, ContractEditorRow Row);
    readonly object gate = new();
    readonly Dictionary<Identity, LinkedListNode<Entry>> entries = new();
    readonly LinkedList<Entry> oldest = new();
    readonly long maximumBytes;
    readonly int maximumEntries;
    long bytes, encodes;
    bool disposed;
    WeakReference<IReadOnlyList<StrategyTradePlanSnapshot>>? lastSource;
    TradePlanHistoryScope lastScope;
    string? lastCulture;
    ContractEditorRow[]? lastRows;

    /// <summary>Creates a workspace cache with aggregate limits across all its trade tabs.</summary>
    public TradePlanRowCache(long maximumBytes = 8 * 1024 * 1024, int maximumEntries = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumEntries, 1);
        this.maximumBytes = maximumBytes; this.maximumEntries = maximumEntries;
    }
    public long Encodes { get { lock (gate) return encodes; } }
    public long EncodedBytes { get { lock (gate) return bytes; } }
    public int Count { get { lock (gate) return entries.Count; } }

    /// <summary>Reuses immutable payloads while retaining the current plan:index wire identifiers.</summary>
    public ContractEditorRow[] Rows(IReadOnlyList<StrategyTradePlanSnapshot> plans, TradePlanHistoryScope scope)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var culture = CultureInfo.CurrentCulture.Name;
            if (lastRows is not null && lastScope == scope && lastCulture == culture
                && lastSource!.TryGetTarget(out var prior) && ReferenceEquals(prior, plans)) return lastRows;
            var visible = new HashSet<Identity>();
            var rows = new ContractEditorRow[plans.Count];
            var allCached = true;
            for (var index = 0; index < plans.Count; index++)
            {
                var plan = plans[index];
                var id = new Identity(scope, plan.Position.Id, plan.ValueDate, plan.Position.RouteGeneration, plan.PlanRevision, culture);
                visible.Add(id);
                if (entries.TryGetValue(id, out var node)
                    && node.Value.Source.TryGetTarget(out var source) && ReferenceEquals(source, plan))
                {
                    oldest.Remove(node); oldest.AddLast(node);
                    var rowId = "plan:" + index.ToString(CultureInfo.InvariantCulture);
                    if (node.Value.Row.Id != rowId) node.Value = node.Value with { Row = node.Value.Row with { Id = rowId } };
                    rows[index] = node.Value.Row;
                    continue;
                }
                if (node is not null) Remove(node);
                var data = TradePlanDisplay.EncodeHistory(plan); encodes++;
                var row = new ContractEditorRow("plan:" + index.ToString(CultureInfo.InvariantCulture),
                    new() { ["kind"] = "plan", ["actionDateTimeEastern"] = TradePlanDisplay.ActionTime(plan) ?? "" }) { Data = data };
                rows[index] = row;
                if (data.LongLength > maximumBytes) { allCached = false; continue; }
                var added = oldest.AddLast(new Entry(id, new(plan), row));
                entries.Add(id, added); bytes += data.LongLength;
            }
            // Keep the visible window for this trade only. Other tabs share the aggregate LRU budget.
            for (var node = oldest.First; node is not null;)
            {
                var next = node.Next;
                if (node.Value.Id.Scope.Position == scope.Position && !visible.Contains(node.Value.Id)) Remove(node);
                node = next;
            }
            while (bytes > maximumBytes || entries.Count > maximumEntries)
            { allCached = false; Remove(oldest.First!); }
            lastRows = allCached ? rows : null;
            lastSource = allCached ? new(plans) : null;
            lastScope = scope; lastCulture = culture;
            return rows;
        }
    }

    /// <summary>Releases one closed trade's rows, including all dates and live/history modes.</summary>
    public void Clear(StrategyPositionId position)
    {
        lock (gate)
        {
            for (var node = oldest.First; node is not null;)
            { var next = node.Next; if (node.Value.Id.Scope.Position == position) Remove(node); node = next; }
            if (lastScope.Position == position) { lastRows = null; lastSource = null; }
        }
    }
    void Remove(LinkedListNode<Entry> node)
    { entries.Remove(node.Value.Id); bytes -= node.Value.Row.Data!.LongLength; oldest.Remove(node); }
    public void Dispose()
    {
        lock (gate) { disposed = true; entries.Clear(); oldest.Clear(); bytes = 0; lastRows = null; lastSource = null; }
    }
}
