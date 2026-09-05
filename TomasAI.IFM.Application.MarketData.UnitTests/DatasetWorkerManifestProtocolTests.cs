using System.Buffers.Binary;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetWorkerManifestProtocolTests
{
    const int MaximumFrameBytes = 256 * 1024;

    [Theory]
    [InlineData(DatasetWorkerMessageKind.StartManifest)]
    [InlineData(DatasetWorkerMessageKind.ApplySubscriptionManifest)]
    [InlineData(DatasetWorkerMessageKind.CooperativeReset)]
    public async Task Complete_manifest_roundtrip_preserves_identity_correlation_provider_mapping_and_roles(
        DatasetWorkerMessageKind kind)
    {
        using var stream = new MemoryStream();
        var frame = Command(kind);

        await DatasetWorkerFrameCodec.WriteAsync(stream, frame, MaximumFrameBytes, CancellationToken.None);
        stream.Position = 0;
        var decoded = await DatasetWorkerFrameCodec.ReadAsync(stream, MaximumFrameBytes, CancellationToken.None);

        decoded.ProtocolMajor.Should().Be(2);
        decoded.Kind.Should().Be(kind);
        decoded.CorrelationId.Should().Be(frame.CorrelationId);
        decoded.WorkerInstanceId.Should().Be(frame.WorkerInstanceId);
        decoded.GenerationId.Should().Be(frame.GenerationId);
        decoded.ManifestRevision.Should().Be(17);
        decoded.ManifestFingerprint.Should().Be(frame.ManifestFingerprint);
        decoded.Manifest!.GetRegistrations().Should().BeEquivalentTo(frame.Manifest!.GetRegistrations());
        decoded.Manifest.Contracts.Single().ProviderContractName.Should().Be("ESZ6");
        decoded.Manifest.Contracts.Single().DomainContractId.Should().Be("ES20261218");
        decoded.Manifest.Contracts.Single().OnTheRun.Should().BeFalse();
        decoded.Manifest.Contracts.Single().Rollover.Should().BeTrue();
    }

    [Theory]
    [InlineData(DatasetWorkerMessageKind.StartManifest)]
    [InlineData(DatasetWorkerMessageKind.ApplySubscriptionManifest)]
    [InlineData(DatasetWorkerMessageKind.CooperativeReset)]
    public async Task Reconstruction_command_without_full_manifest_is_rejected_before_writing(
        DatasetWorkerMessageKind kind)
    {
        using var stream = new MemoryStream();
        var frame = Command(kind) with { Manifest = null };

        var write = () => DatasetWorkerFrameCodec.WriteAsync(stream, frame,
            MaximumFrameBytes, CancellationToken.None).AsTask();

        await write.Should().ThrowAsync<InvalidDataException>();
        stream.Length.Should().Be(0);
    }

    [Theory]
    [InlineData("dataset")]
    [InlineData("value-date")]
    [InlineData("revision")]
    [InlineData("fingerprint")]
    public async Task Reader_rejects_manifest_that_does_not_match_enclosing_command(string mismatch)
    {
        var frame = Command(DatasetWorkerMessageKind.StartManifest);
        frame = mismatch switch
        {
            "dataset" => frame with { Dataset = "XNAS.ITCH" },
            "value-date" => frame with { ValueDate = frame.ValueDate.AddDays(1) },
            "revision" => frame with { ManifestRevision = frame.ManifestRevision + 1 },
            _ => frame with { ManifestFingerprint = new string('0', 64) }
        };
        using var stream = EncodeUnchecked(frame);

        var read = () => DatasetWorkerFrameCodec.ReadAsync(stream,
            MaximumFrameBytes, CancellationToken.None).AsTask();

        await read.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData(DatasetWorkerMessageKind.StartAccepted)]
    [InlineData(DatasetWorkerMessageKind.SubscriptionManifestApplied)]
    [InlineData(DatasetWorkerMessageKind.ResetCompleted)]
    public async Task Realized_acknowledgement_requires_manifest_fingerprint(DatasetWorkerMessageKind kind)
    {
        using var stream = new MemoryStream();
        var frame = Command(kind) with { Manifest = null, ManifestFingerprint = string.Empty, Healthy = true };

        var write = () => DatasetWorkerFrameCodec.WriteAsync(stream, frame,
            MaximumFrameBytes, CancellationToken.None).AsTask();

        await write.Should().ThrowAsync<InvalidDataException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task Legacy_protocol_is_rejected_instead_of_starting_without_parent_owned_manifest()
    {
        using var stream = EncodeUnchecked(Command(DatasetWorkerMessageKind.WorkerHello) with
        {
            ProtocolMajor = 1,
            Manifest = null,
            ManifestRevision = 0,
            ManifestFingerprint = string.Empty
        });

        var read = () => DatasetWorkerFrameCodec.ReadAsync(stream,
            MaximumFrameBytes, CancellationToken.None).AsTask();

        await read.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Oversized_frame_is_rejected_before_any_bytes_are_written()
    {
        using var stream = new MemoryStream();

        var write = () => DatasetWorkerFrameCodec.WriteAsync(stream,
            Command(DatasetWorkerMessageKind.StartManifest), 32, CancellationToken.None).AsTask();

        await write.Should().ThrowAsync<InvalidDataException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task Oversized_declared_frame_is_rejected_before_payload_allocation_or_read()
    {
        using var stream = new MemoryStream();
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, MaximumFrameBytes + 1);
        stream.Write(prefix);
        stream.Position = 0;

        var read = () => DatasetWorkerFrameCodec.ReadAsync(stream,
            MaximumFrameBytes, CancellationToken.None).AsTask();

        await read.Should().ThrowAsync<InvalidDataException>();
        stream.Position.Should().Be(4);
    }

    static DatasetWorkerControlFrame Command(DatasetWorkerMessageKind kind)
    {
        var manifest = new DatasetSubscriptionManifest("GLBX.MDP3", new(2026, 9, 4), 17,
        [new DatasetSubscriptionContract
        {
            DomainContractId = "ES20261218",
            ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures,
            RootSymbol = "ES",
            Dataset = "GLBX.MDP3",
            OnTheRun = false,
            Rollover = true
        }]);
        return new()
        {
            Kind = kind,
            WorkerInstanceId = Guid.NewGuid(),
            Dataset = manifest.Dataset,
            ValueDate = manifest.ValueDate,
            GenerationId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            Sequence = 2,
            BootstrapToken = new string('A', 64),
            Manifest = manifest,
            ManifestRevision = manifest.Revision,
            ManifestFingerprint = manifest.Fingerprint
        };
    }

    // Deliberately bypass the sender's validation to prove the receiving trust boundary.
    static MemoryStream EncodeUnchecked(DatasetWorkerControlFrame frame)
    {
        var payload = MessagePackSerializer.Serialize(frame);
        var stream = new MemoryStream();
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        stream.Write(prefix);
        stream.Write(payload);
        stream.Position = 0;
        return stream;
    }
}
