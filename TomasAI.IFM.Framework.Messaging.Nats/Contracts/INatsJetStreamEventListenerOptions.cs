namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

/// <summary>
/// Configures a durable JetStream actor-event listener.
/// </summary>
public interface INatsJetStreamEventListenerOptions : INatsEventListenerOptions
{
    string StreamName { get; set; }
    string DurableConsumerNamePrefix { get; set; }
    string FilterSubject { get; set; }
    NatsJetStreamEventDeliverPolicy DeliverPolicy { get; set; }
    TimeSpan AckWait { get; set; }
    int MaxDeliver { get; set; }
    int DispatcherCount { get; set; }
    int DispatcherCapacity { get; set; }
    int MaxAckPending { get; set; }
    int MaxMessages { get; set; }
    int ThresholdMessages { get; set; }
    TimeSpan NegativeAcknowledgeDelay { get; set; }
}
