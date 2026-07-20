namespace TomasAI.IFM.Framework.Messaging.Nats.Contracts;

/// <summary>
/// Represents configuration options for a NATS event listener.
/// </summary>
public interface INatsEventListenerOptions
{
    string Url { get; set; }
}
