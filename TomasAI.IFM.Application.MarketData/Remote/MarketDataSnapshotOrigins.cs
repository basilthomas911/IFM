using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Preserves creation provenance through reconnect snapshots without altering publication bodies.</summary>
public static class MarketDataSnapshotOrigins
{
    /// <summary>Copies known origins beside the exact captured publication identities.</summary>
    public static MarketDataReadSnapshot Capture(MarketDataReadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CurrentValues is null)
            throw new InvalidDataException("Missing current publications in origin snapshot.");
        var origins = Frames(snapshot).Select(frame => (Frame: frame, Origin: MessageOrigins.Get(frame)))
            .Where(value => value.Origin is not null)
            .DistinctBy(value => Key(value.Frame))
            .Select(value => new DatasetPublicationOrigin(value.Frame.Dataset, value.Frame.WorkerInstanceId,
                value.Frame.GenerationId, value.Frame.PublicationSequence, MessageOriginJson.Serialize(value.Origin)!))
            .ToArray();
        return snapshot with { PublicationOrigins = origins.Length == 0 ? null : origins };
    }

    /// <summary>Validates provenance before a mirror applies any state. Old snapshots remain explicitly unknown.</summary>
    public static void Restore(MarketDataReadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CurrentValues is null)
            throw new InvalidDataException("Missing current publications in origin snapshot.");
        if (snapshot.PublicationOrigins is { Length: > 8192 })
            throw new InvalidDataException("Publication origin snapshot exceeds its capacity.");
        var origins = new Dictionary<(string, Guid, Guid, long), MessageOrigin>();
        foreach (var value in snapshot.PublicationOrigins ?? [])
        {
            if (value is null || string.IsNullOrWhiteSpace(value.Dataset) || value.WorkerInstanceId == Guid.Empty
                || value.GenerationId == Guid.Empty || value.PublicationSequence <= 0)
                throw new InvalidDataException("Invalid publication origin identity.");
            var origin = MessageOriginJson.Deserialize(value.OriginJson)
                ?? throw new InvalidDataException("Missing publication origin.");
            if (origin.Generation is { } generation && generation != value.GenerationId)
                throw new InvalidDataException("Publication origin generation differs from its envelope.");
            var key = (value.Dataset, value.WorkerInstanceId, value.GenerationId, value.PublicationSequence);
            if (!origins.TryAdd(key, origin)) throw new InvalidDataException("Duplicate publication origin identity.");
        }
        var frames = Frames(snapshot).ToArray();
        var identities = frames.Select(Key).ToHashSet();
        if (origins.Keys.Any(key => !identities.Contains(key)))
            throw new InvalidDataException("Publication origin has no matching captured frame.");
        foreach (var frame in frames)
            if (MessageOrigins.Get(frame) is { } existing
                && origins.GetValueOrDefault(Key(frame)) is { } incoming && incoming != existing)
                throw new InvalidDataException("Publication origin conflicts with an already captured frame.");
        foreach (var frame in frames)
            MessageOrigins.Restore(frame, origins.GetValueOrDefault(Key(frame)));
    }

    static (string, Guid, Guid, long) Key(DatasetPublicationEnvelope frame)
        => (frame.Dataset, frame.WorkerInstanceId, frame.GenerationId, frame.PublicationSequence);

    static IEnumerable<DatasetPublicationEnvelope> Frames(MarketDataReadSnapshot snapshot)
        => snapshot.CurrentValues.Concat(snapshot.OptionObservations ?? []);
}
