namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Defines the loss and retry contract for Core NATS messages that do not
/// contain a reply subject. Core NATS cannot redeliver a message after local
/// consumption, so enforcement requires an explicit classification.
/// </summary>
public enum CoreNatsTrafficClass
{
    Unknown,
    RequestReplyOnly,
    Optional,
    RequiredNonDurable
}
