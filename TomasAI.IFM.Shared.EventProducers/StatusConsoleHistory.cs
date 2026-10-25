using System.Text.Json;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.Shared.EventProducers;

/// <summary>Durable console history, independent of UI lifetime and market sessions.</summary>
public static class StatusConsoleHistory
{
    public const string StreamName = "IFM_STATUS_CONSOLE_HISTORY";
    static readonly NatsByteArrayMessageSerializer Bytes = new();

    // Create only when absent: readers and writers never reset retention or delete history.
    public static async ValueTask<INatsJSStream> EnsureAsync(INatsJSContext js, CancellationToken cancellationToken = default)
    {
        try { return await js.GetStreamAsync(StreamName, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (NatsJSApiException error) when (error.Error.Code == 404)
        {
            try
            {
                return await js.CreateStreamAsync(new StreamConfig(StreamName, ["Notify.StatusConsoleEvent.Logged.>"])
                {
                    Storage = StreamConfigStorage.File,
                    Retention = StreamConfigRetention.Limits,
                    MaxMsgs = -1, MaxBytes = -1, MaxAge = TimeSpan.Zero
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (NatsJSApiException)
            {
                // Another producer can create it concurrently; only a successful read resolves the race.
                return await js.GetStreamAsync(StreamName, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public sealed record Entry(ulong Sequence, Guid Id, DateTime StatusDate, string Message);
    public sealed record Page(ulong NextSequence, ulong ThroughSequence, bool HasMore, IReadOnlyList<Entry> Messages);

    /// <summary>Reads a bounded page through a fixed watermark. Reading never acknowledges or removes messages.</summary>
    public static async Task<Page> ReadAsync(NatsConnectionManager connection, INatsProducerOptions options,
        ulong after, ulong? through, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit));
        var js = await connection.GetJetStreamContextAsync(options.Url, cancellationToken).ConfigureAwait(false);
        var stream = await EnsureAsync(js, cancellationToken).ConfigureAwait(false);
        var last = stream.Info.State.LastSeq;
        var end = through ?? last;
        if (end > last || after > end) throw new ArgumentException("Invalid console history cursor.");
        var client = await connection.GetClientAsync(options.Url, cancellationToken).ConfigureAwait(false);
        var rows = new List<Entry>();
        var pageBytes = 0;
        var cursor = after;
        while (cursor < end && rows.Count < limit)
        {
            var response = await client.RequestAsync<byte[], byte[]>("$JS.API.STREAM.MSG.GET." + StreamName,
                JsonSerializer.SerializeToUtf8Bytes(new { seq = cursor + 1 }),
                requestSerializer: Bytes, replySerializer: Bytes, cancellationToken: cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(response.Data ?? throw new InvalidOperationException("Empty console history response."));
            if (document.RootElement.TryGetProperty("error", out var error))
                throw new InvalidOperationException("Console history read failed: " + error);
            var stored = document.RootElement.GetProperty("message");
            if (stored.GetProperty("seq").GetUInt64() != cursor + 1)
                throw new InvalidOperationException("Console history returned a non-contiguous sequence.");
            var data = stored.GetProperty("data").GetBytesFromBase64();
            var message = NatsMessagePackSerializer<StatusConsoleLoggedEvent>.Default.Deserialize(new System.Buffers.ReadOnlySequence<byte>(data))
                ?? throw new InvalidOperationException("Invalid console history message.");
            var sequence = stored.GetProperty("seq").GetUInt64();
            var entry = new Entry(sequence, message.Id, message.StatusConsoleLog.StatusDate, message.StatusConsoleLog.Message);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(entry).Length;
            // Bound ordinary pages by bytes as well as count. A single large
            // broker message still gets its own page, without truncating its text.
            if (rows.Count > 0 && pageBytes + bytes > 1024 * 1024) break;
            pageBytes += bytes;
            cursor = sequence;
            rows.Add(entry);
        }
        return new(cursor, end, cursor < end, rows);
    }
}
