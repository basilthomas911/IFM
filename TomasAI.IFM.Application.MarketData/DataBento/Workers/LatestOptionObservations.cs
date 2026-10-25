using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Bounded latest values, not a recovery backlog. Payload timestamps are never changed.</summary>
public sealed class LatestOptionObservations
{
    readonly object gate = new();
    readonly Dictionary<string, Entry> latest = new(StringComparer.Ordinal);
    readonly HashSet<Guid> retired = [];
    readonly Queue<Guid> retiredOrder = new();
    Guid generation;
    int cursor;
    public bool Retain(DatasetPublicationEnvelope frame) => Retain(frame, out _);
    public bool Retain(DatasetPublicationEnvelope frame, out FuturesOptionTickDataV2ReadModel? observation)
    {
        observation = null;
        if (frame.Kind != DatasetPublicationKind.OptionQuoteObservation) return false;
        var tick = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(frame.Payload).OptionMarketPriceObservation?.OptionTickData
            ?? throw new InvalidDataException("Option observation payload is missing.");
        if (tick.ValueDate != frame.ValueDate || tick.SourceGeneration != frame.GenerationId)
            throw new InvalidDataException("Option observation identity differs from its envelope.");
        observation = tick;
        lock (gate)
        {
            if (retired.Contains(frame.GenerationId)) return false;
            if (generation != frame.GenerationId)
            {
                if (generation != Guid.Empty) { retired.Add(generation); retiredOrder.Enqueue(generation); }
                if (retiredOrder.Count > 64) retired.Remove(retiredOrder.Dequeue());
                generation = frame.GenerationId; latest.Clear(); cursor = 0;
            }
            if (!latest.ContainsKey(tick.ContractId) && latest.Count >= 2048) throw new InvalidOperationException("Option latest-value capacity exceeded.");
            if (latest.TryGetValue(tick.ContractId, out var old) && old.Frame.GenerationId == frame.GenerationId
                && (old.Frame.PublicationSequence >= frame.PublicationSequence || tick.ObservedAtUtc < old.ObservedAt
                    || tick.ObservedAtUtc == old.ObservedAt && tick.CalculatedAtUtc < old.CalculatedAt)) return false;
            latest[tick.ContractId] = new(frame, tick.ObservedAtUtc, tick.CalculatedAtUtc);
            return true;
        }
    }
    sealed record Entry(DatasetPublicationEnvelope Frame, DateTimeOffset ObservedAt, DateTimeOffset CalculatedAt);
    public DatasetPublicationEnvelope[] Capture()
    {
        lock (gate)
        {
            if (latest.Count == 0) return [];
            var values = latest.Values.Select(entry => entry.Frame).ToArray();
            var result = new List<DatasetPublicationEnvelope>(); var bytes = 0;
            for (var i = 0; i < Math.Min(values.Length, 128); i++)
            {
                var value = values[cursor++ % values.Length];
                if (bytes + value.Payload.Length > 512 * 1024) break;
                result.Add(value); bytes += value.Payload.Length;
            }
            cursor %= values.Length;
            return result.OrderBy(value => value.PublicationSequence).ToArray();
        }
    }
}
