namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

/// <summary>
/// Specifies where a newly created durable listener begins consuming its stream.
/// </summary>
public enum NatsJetStreamEventDeliverPolicy
{
    All = 0,
    New = 1
}
