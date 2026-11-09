using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataSnapshotOriginTests
{
    [Fact]
    public void ReconnectSnapshotRoundTripKeepsOriginalOriginAndEightKeyFrameBody()
    {
        var frame = Frame();
        var origin = new MessageOrigin(new DateTimeOffset(2025, 11, 3, 12, 0, 0, TimeSpan.Zero),
            "system", generation: frame.GenerationId);
        var originalBody = MarketDataHostConnection.Encode(frame);
        MessageOrigins.Restore(frame, origin);
        var snapshot = MarketDataSnapshotOrigins.Capture(Snapshot(frame));
        var decoded = MarketDataHostConnection.Decode<MarketDataReadSnapshot>(MarketDataHostConnection.Encode(snapshot));
        MarketDataSnapshotOrigins.Restore(decoded);
        Assert.Equal(origin, MessageOrigins.Get(Assert.Single(decoded.CurrentValues)));
        Assert.Equal(originalBody, MarketDataHostConnection.Encode(decoded.CurrentValues[0]));
    }

    [Fact]
    public void OldSnapshotKeepsUnknownOriginWhenForwarded()
    {
        var snapshot = Snapshot(Frame());
        MarketDataSnapshotOrigins.Restore(snapshot);
        var value = Assert.Single(snapshot.CurrentValues);
        Assert.Null(MessageOrigins.Get(value));
        Assert.Null(MessageOrigins.ForPublication(value, TimeProvider.System));
    }

    [Fact]
    public void SnapshotRejectsOriginForDifferentGeneration()
    {
        var frame = Frame();
        var metadata = new MessageOrigin(DateTimeOffset.UtcNow, "system", generation: Guid.NewGuid());
        var snapshot = Snapshot(frame) with { PublicationOrigins =
            [new(frame.Dataset, frame.WorkerInstanceId, frame.GenerationId, frame.PublicationSequence,
                MessageOriginJson.Serialize(metadata)!)] };
        Assert.Throws<InvalidDataException>(() => MarketDataSnapshotOrigins.Restore(snapshot));
        Assert.Null(MessageOrigins.Get(frame));
    }

    static MarketDataReadSnapshot Snapshot(DatasetPublicationEnvelope frame)
        => new(new MarketDataBackendStatus(false, frame.ValueDate, []), [], [], [frame]);

    static DatasetPublicationEnvelope Frame() => new()
    {
        Dataset = "GLBX.MDP3", ValueDate = new DateOnly(2025, 11, 3), WorkerInstanceId = Guid.NewGuid(),
        GenerationId = Guid.NewGuid(), ManifestRevision = 1, PublicationSequence = 1,
        Kind = DatasetPublicationKind.MarketPrice, Payload = [0xc0]
    };
}
