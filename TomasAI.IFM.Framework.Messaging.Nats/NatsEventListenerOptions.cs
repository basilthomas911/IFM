using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Represents the configuration options for a NATS event listener.
/// </summary>
public class NatsEventListenerOptions : INatsEventListenerOptions
{
    public string Url { get; set; } = Environment.GetEnvironmentVariable("IFM_NATS_URL")
        ?? "nats://localhost:4222";
}
