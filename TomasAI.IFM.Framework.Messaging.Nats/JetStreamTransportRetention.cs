using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

internal static class JetStreamTransportRetention
{
    internal static StreamConfig CreateFanOut(string streamName, string subject) =>
        new(streamName, [subject])
        {
            Retention = StreamConfigRetention.Interest
        };

    internal static StreamConfig CreateQueue(string streamName, string subject) =>
        new(streamName, [subject])
        {
            Retention = StreamConfigRetention.Workqueue
        };

    internal static bool EnsureFanOut(StreamConfig configuration) =>
        Ensure(configuration, StreamConfigRetention.Interest);

    private static bool Ensure(StreamConfig configuration, StreamConfigRetention retention)
    {
        if (configuration.Retention == retention) return false;
        configuration.Retention = retention;
        return true;
    }
}
