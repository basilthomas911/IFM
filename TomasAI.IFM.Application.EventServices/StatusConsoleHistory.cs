using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.Application.EventServices;

/// <summary>Single-desktop console delivery. Messages survive UI disconnects for at most five minutes.</summary>
public static class StatusConsoleHistory
{
    // Separate subject/stream: JetStream cannot change an existing stream's retention policy.
    // This migration never touches trade event-log streams.
    public const string StreamName = "IFM_STATUS_CONSOLE_QUEUE";
    public const string Subject = "Console.StatusConsoleEvent.Logged";
    public const string ConsumerName = "console-ui";
    public const string DeliverySubject = "ConsoleDelivery.SingleUI.messages";
    public const long MaximumMessages = 10_000;
    public const long MaximumBytes = 10 * 1024 * 1024;
    public static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(5);
    static readonly NatsByteArrayMessageSerializer Bytes = new();

    public static async ValueTask<INatsJSStream> EnsureAsync(INatsJSContext js, CancellationToken cancellationToken = default)
    {
        var stream = await js.CreateOrUpdateStreamAsync(new StreamConfig(StreamName, [Subject])
        {
            Storage = StreamConfigStorage.File, Retention = StreamConfigRetention.Workqueue,
            MaxMsgs = MaximumMessages, MaxBytes = MaximumBytes, MaxAge = MaximumAge,
            Discard = StreamConfigDiscard.Old, MaxMsgSize = 64 * 1024
        }, cancellationToken).ConfigureAwait(false);
        // Retire the unlimited broadcast history. Its remaining messages expire within five minutes.
        try
        {
            var legacy = await js.GetStreamAsync("IFM_STATUS_CONSOLE_HISTORY", cancellationToken: cancellationToken).ConfigureAwait(false);
            if (legacy.Info.Config.MaxAge != MaximumAge || legacy.Info.Config.MaxMsgs != MaximumMessages
                || legacy.Info.Config.Subjects.Contains("Notify.StatusConsoleEvent.Logged.>"))
            {
                var config = legacy.Info.Config;
                config.Subjects = ["Retired.IFM.StatusConsoleHistory"];
                config.MaxAge = MaximumAge; config.MaxMsgs = MaximumMessages; config.MaxBytes = MaximumBytes;
                config.Discard = StreamConfigDiscard.Old;
                await js.UpdateStreamAsync(config, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (NatsJSApiException error) when (error.Error.Code == 404) { /* No legacy stream to retire. */ }
        await EnsurePushConsumerAsync(js, cancellationToken).ConfigureAwait(false);
        return stream;
    }

    static async Task EnsurePushConsumerAsync(INatsJSContext js, CancellationToken token)
    {
        try
        {
            var existing = await js.GetConsumerAsync(StreamName, ConsumerName, token).ConfigureAwait(false);
            if (existing.Info.Config.DeliverSubject != DeliverySubject)
                await js.DeleteConsumerAsync(StreamName, ConsumerName, token).ConfigureAwait(false);
        }
        catch (NatsJSApiException error) when (error.Error.Code == 404) { /* First connection. */ }
        await js.CreateOrUpdateConsumerAsync(StreamName, new ConsumerConfig(ConsumerName)
        {
            DeliverSubject = DeliverySubject, FilterSubject = Subject,
            AckPolicy = ConsumerConfigAckPolicy.Explicit, DeliverPolicy = ConsumerConfigDeliverPolicy.All,
            AckWait = TimeSpan.FromSeconds(10), MaxAckPending = 100, MaxDeliver = -1
        }, token).ConfigureAwait(false);
    }

    public sealed record DisplayedBatch(string[] Acknowledgements);

    /// <summary>One UI request acknowledges only its displayed batch. Duplicate acknowledgements are harmless.</summary>
    public static async Task AcknowledgeAsync(NatsConnectionManager connection, INatsProducerOptions options,
        DisplayedBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Acknowledgements is not { Length: > 0 and <= 100 }
            || batch.Acknowledgements.Any(receipt => !IsConsoleReceipt(receipt)))
            throw new ArgumentException("Invalid console acknowledgement batch.", nameof(batch));
        var client = await connection.GetClientAsync(options.Url, cancellationToken).ConfigureAwait(false);
        foreach (var receipt in batch.Acknowledgements.Distinct(StringComparer.Ordinal))
            await client.PublishAsync(receipt, "+ACK"u8.ToArray(), serializer: Bytes,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        // Flush the entire batch before reporting success. Validate receipts before publishing any ACK.
        await client.PingAsync(cancellationToken).ConfigureAwait(false);
    }

    static bool IsConsoleReceipt(string? receipt)
    {
        if (receipt is null || receipt.Length > 512) return false;
        var parts = receipt.Split('.');
        return parts.Length == 9 && parts[0] == "$JS" && parts[1] == "ACK"
            && parts[2] == StreamName && parts[3] == ConsumerName
            && parts.Skip(4).All(value => ulong.TryParse(value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out _));
    }
}
