using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

/// <summary>Bounded process-local evidence. Missing evidence is never a successful check.</summary>
public sealed class LivePipelineEvidence(TimeProvider time)
{
    readonly object gate = new();
    readonly Dictionary<string, LivePipelineCheck> evidence = new(StringComparer.Ordinal);
    readonly Dictionary<Guid, (DateTime Seen, LiveUiHealthReport Report)> clients = new();
    LivePipelineHealthSnapshot? audit;
    public void PublishAudit(LivePipelineHealthSnapshot snapshot) => Volatile.Write(ref audit, snapshot);
    public bool AllowsNewDecisions => Volatile.Read(ref audit) is { } latest
        && time.GetUtcNow().UtcDateTime - latest.ObservedUtc <= TimeSpan.FromSeconds(90)
        && latest.AllowsNewDecisions;
    public void Record(string component, string scope, string status, string reason, DateTime? progress = null)
    {
        lock (gate)
        {
            var key = component + "/" + scope;
            if (evidence.Count >= 256 && !evidence.ContainsKey(key)) return;
            evidence[key] = new(component, scope, status, reason, time.GetUtcNow().UtcDateTime, progress);
        }
    }
    public LivePipelineCheck? Get(string component, string scope)
    {
        lock (gate) return evidence.GetValueOrDefault(component + "/" + scope);
    }
    public bool ReportUi(LiveUiHealthReport report)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (report.SiteId == Guid.Empty || report.Streams is null || report.Streams.Count > 3
            || report.Streams.Any(x => x is null || x.ContractId is null || x.ContractId.Length > 128 || x.Symbol is not ("ES" or "VX" or "Outlook")
                || x.ReceivedBarUtc > now.AddSeconds(5) || (x.RenderedBarUtc.HasValue && !x.ReceivedBarUtc.HasValue) || x.RenderedBarUtc > x.ReceivedBarUtc)
            || report.Streams.Select(x => x.Symbol).Distinct().Count() != report.Streams.Count) return false;
        lock (gate)
        {
            foreach (var id in clients.Where(x => now - x.Value.Seen > TimeSpan.FromHours(1)).Select(x => x.Key).ToArray()) clients.Remove(id);
            if (clients.Count >= 32 && !clients.ContainsKey(report.SiteId)) return false;
            clients[report.SiteId] = (now, report);
            return true;
        }
    }
    public IReadOnlyList<LivePipelineCheck> CheckUi(DateOnly date, IReadOnlyDictionary<string, DateTime> latest,
        IReadOnlyDictionary<string, string>? contracts = null)
    {
        var now = time.GetUtcNow().UtcDateTime;
        lock (gate)
        {
            if (clients.Count == 0) return [new("UI", "clients", "Unknown", "No UI receipt/render evidence is available.", now, Required: false)];
            var results = new List<LivePipelineCheck>();
            foreach (var (id, client) in clients)
            {
                var scope = id.ToString("N");
                if (now - client.Seen > TimeSpan.FromMinutes(2))
                {
                    results.Add(new("UI heartbeat", scope, "Unknown", "UI disconnected or heartbeat overdue.", now, client.Seen, false));
                    continue;
                }
                if (client.Report.ValueDate != date)
                {
                    results.Add(new("UI value date", scope, "Degraded", "Connected UI is displaying a different value date.", now, client.Seen));
                    continue;
                }
                foreach (var (symbol, upstream) in latest)
                {
                    var stream = client.Report.Streams.FirstOrDefault(x => x.Symbol == symbol);
                    if (contracts?.TryGetValue(symbol, out var expectedContract) == true && stream?.ContractId != expectedContract)
                    {
                        results.Add(new("UI contract", scope + "/" + symbol, "Degraded", "UI is not displaying the authoritative current contract.", now));
                        continue;
                    }
                    Check("UI delivery", stream?.ReceivedBarUtc);
                    Check("UI rendering", stream?.RenderedBarUtc);
                    void Check(string component, DateTime? value) => results.Add(new(component, scope + "/" + symbol,
                        value is not null && upstream - value.Value <= TimeSpan.FromSeconds(90) ? "Healthy" : "Degraded",
                        value is null ? "No acknowledgement for current output." : "Compared acknowledged output timestamp with backend output.", now, value));
                }
            }
            return results;
        }
    }
}
