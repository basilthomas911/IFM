using TomasAI.IFM.Framework.Messaging.Nats.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Represents the configuration options for a NATS event listener.
/// </summary>
public class NatsEventListenerOptions : INatsEventListenerOptions
{
    public string Url { get; set; } = "nats://localhost:4222";
}
