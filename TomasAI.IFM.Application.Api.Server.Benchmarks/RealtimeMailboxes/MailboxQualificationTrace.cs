using System.Diagnostics.Tracing;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[EventSource(Name = "IFM-Mailbox-Qualification")]
public sealed class MailboxQualificationTrace : EventSource
{
    public static MailboxQualificationTrace Log { get; } = new();
    [Event(1)] public void WorkloadStart(string name) => WriteEvent(1, name);
    [Event(2)] public void WorkloadStop(string name) => WriteEvent(2, name);
}
