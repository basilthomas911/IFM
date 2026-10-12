namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

/// <summary>
/// Represents configuration options for a NATS event listener.
/// </summary>
public interface INatsEventListenerOptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    string Url { get; set; }
}
