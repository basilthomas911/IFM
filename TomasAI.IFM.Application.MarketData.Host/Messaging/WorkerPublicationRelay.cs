using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Forwards replaceable events through Core NATS and waits for persisted evidence receipts only.</summary>
internal sealed class WorkerPublicationRelay(MarketDataHostConnection connection, string role, Guid worker,
    TomasAI.IFM.Application.MarketData.Pricing.IOptionTradeEvidenceWriter? evidenceWriter = null, ILogger? logger = null)
{
    readonly CancellationTokenSource closed = new();
    readonly SemaphoreSlim publicationGate = new(1, 1);
    long publicationSequence;
    readonly LatestOptionObservations observations = new();
    readonly HashSet<string> firstQuotes = new(StringComparer.Ordinal), firstRisks = new(StringComparer.Ordinal);
    Guid loggedGeneration;
    internal DatasetPublicationEnvelope[] CaptureObservations() => observations.Capture();
    internal async ValueTask PublishAsync(DatasetPublicationEnvelope envelope, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, closed.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var client = await connection.GetClientAsync(deadline.Token).ConfigureAwait(false);
        if (envelope.Kind == DatasetPublicationKind.OptionTradeEvidence)
        {
            if (evidenceWriter is not null)
            {
                var evidence = MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Pricing.OptionTradeEvidence>(envelope.Payload);
                evidence.Validate();
                if (evidence.Source.Dataset != envelope.Dataset || evidence.Source.GenerationId != envelope.GenerationId
                    || evidence.Source.ValueDate != envelope.ValueDate) throw new InvalidDataException("Option evidence identity differs from the owned worker envelope.");
                await evidenceWriter.WriteAsync(evidence, deadline.Token).ConfigureAwait(false);
                await ForwardAsync(envelope, deadline.Token).ConfigureAwait(false);
                return;
            }
            var inbox = "_INBOX." + Guid.NewGuid().ToString("N");
            await using var receipts = await client.Connection.SubscribeCoreAsync<bool>(inbox, cancellationToken: deadline.Token).ConfigureAwait(false);
            await ForwardAsync(envelope, deadline.Token, inbox).ConfigureAwait(false);
            var receipt = await receipts.Msgs.ReadAsync(deadline.Token).ConfigureAwait(false);
            receipt.EnsureSuccess();
            if (!receipt.Data) throw new InvalidDataException("Option evidence persistence was not acknowledged.");
        }
        else await ForwardAsync(envelope, deadline.Token).ConfigureAwait(false);
    }
    async Task ForwardAsync(DatasetPublicationEnvelope envelope, CancellationToken token, string? replyTo = null)
    {
        await publicationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // Evidence can finish persistence after later quotes. The relay owns delivery order;
            // native evidence identity/timestamps remain untouched in the payload.
            if (role == "Options") envelope = envelope with { PublicationSequence = ++publicationSequence };
            // Retain before transmission so an early or failed delivery can be caught up after admission.
            if (role == "Options" && observations.Retain(envelope, out var retained))
            {
                if (loggedGeneration != envelope.GenerationId) { loggedGeneration = envelope.GenerationId; firstQuotes.Clear(); firstRisks.Clear(); }
                var tick = retained!;
                var firstQuote = firstQuotes.Add(tick.ContractId);
                var firstRisk = tick.GreeksAvailable == true && firstRisks.Add(tick.ContractId);
                if (firstQuote || firstRisk) logger?.LogInformation("Option first observation received by Host; Contract={Contract}; Generation={Generation}; QuoteUtc={QuoteUtc}; RiskUtc={RiskUtc}; QualifiedRisk={QualifiedRisk}; Sequence={Sequence}",
                    tick.ContractId, envelope.GenerationId, tick.ObservedAtUtc, tick.CalculatedAtUtc, tick.GreeksAvailable, envelope.PublicationSequence);
            }
            var client = await connection.GetClientAsync(token).ConfigureAwait(false);
            await client.PublishAsync(connection.Publications(role, worker), envelope,
                serializer: NatsMessagePackSerializer<DatasetPublicationEnvelope>.Default, replyTo: replyTo, cancellationToken: token).ConfigureAwait(false);
        }
        finally { publicationGate.Release(); }
    }
    internal void Close() => _ = closed.CancelAsync();
}
