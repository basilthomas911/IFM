using System.Diagnostics.Tracing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime;

/// <summary>Opt-in process trace events for verifying cache reuse without logging policy or calendar payloads.</summary>
[EventSource(Name = "TomasAI-WorkflowReadCache")]
public sealed class WorkflowReadCacheDiagnostics : EventSource
{
    public static readonly WorkflowReadCacheDiagnostics Log = new();
    private WorkflowReadCacheDiagnostics() { }
    [Event(1, Level = EventLevel.Informational)]
    public void ConfigurationLoad() { if (IsEnabled()) WriteEvent(1); }
    [Event(2, Level = EventLevel.Informational)]
    public void ConfigurationReuse() { if (IsEnabled()) WriteEvent(2); }
    [Event(3, Level = EventLevel.Informational)]
    public void CalendarLoad() { if (IsEnabled()) WriteEvent(3); }
    [Event(4, Level = EventLevel.Informational)]
    public void CalendarReuse() { if (IsEnabled()) WriteEvent(4); }
}
