using MessagePack;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.MarketData.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionRecoveryReplacementProofTests
{
    static readonly DateOnly Date = new(2026, 10, 27);

    [Theory]
    [InlineData("valid", true)]
    [InlineData("cleanup", false)]
    [InlineData("owner", false)]
    [InlineData("generation", false)]
    [InlineData("result-owner", false)]
    [InlineData("result-generation", false)]
    [InlineData("operation", false)]
    [InlineData("date", false)]
    [InlineData("host", false)]
    [InlineData("capability", false)]
    [InlineData("health", false)]
    [InlineData("restoration", false)]
    [InlineData("request-owner", false)]
    [InlineData("request-generation", false)]
    [InlineData("request-role", false)]
    [InlineData("pid", false)]
    public void Native_same_PID_requires_complete_matching_replacement_proof(string mode, bool accepted)
    {
        var old = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 100, true, true, "Healthy", null, Date);
        var next = old with { WorkerInstanceId = Guid.NewGuid(), GenerationId = Guid.NewGuid() };
        var request = new MarketDataRecoveryRequest(Guid.NewGuid(), old.Role, old.Dataset, old.WorkerInstanceId, old.GenerationId, Date, "test");
        var before = new MarketDataBackendStatus(true, Date, [old], Guid.NewGuid(), RecoveryOwnership: MarketDataRecoveryOwnership.TokioDatasetOwner);
        var after = before with { Datasets = [next] };
        var result = new MarketDataRecoveryResult(request.OperationId, old.Role, old.Dataset, true, true, true, null, next.WorkerInstanceId, next.GenerationId);
        switch (mode)
        {
            case "cleanup": result = result with { CleanupConfirmed = false }; break;
            case "owner": next = next with { WorkerInstanceId = old.WorkerInstanceId }; result = result with { WorkerInstanceId = next.WorkerInstanceId }; break;
            case "generation": next = next with { GenerationId = old.GenerationId }; result = result with { GenerationId = next.GenerationId }; break;
            case "result-owner": result = result with { WorkerInstanceId = Guid.NewGuid() }; break;
            case "result-generation": result = result with { GenerationId = Guid.NewGuid() }; break;
            case "operation": result = result with { OperationId = Guid.NewGuid() }; break;
            case "date": next = next with { ValueDate = Date.AddDays(-1) }; break;
            case "host": after = after with { HostInstanceId = Guid.NewGuid() }; break;
            case "capability": after = after with { RecoveryOwnership = MarketDataRecoveryOwnership.Process }; break;
            case "health": next = next with { Healthy = false }; break;
            case "restoration": result = result with { LiveFeedEnabled = false }; break;
            case "request-owner": request = request with { ExpectedWorkerInstanceId = Guid.NewGuid() }; break;
            case "request-generation": request = request with { ExpectedGenerationId = Guid.NewGuid() }; break;
            case "request-role": request = request with { Role = "Futures" }; break;
            case "pid": next = next with { ProcessId = 200 }; break;
        }
        var error = Record.Exception(() => OptionRecoveryReplacementProof.Validate(before, old, after, next, request, result));
        if (accepted) Assert.Null(error); else Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void Current_backend_still_requires_a_changed_process()
    {
        var old = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 100, true, true, "Healthy", null, Date);
        var status = new MarketDataBackendStatus(true, Date, [old]);
        var request = new MarketDataRecoveryRequest(Guid.NewGuid(), old.Role, old.Dataset, old.WorkerInstanceId, old.GenerationId, Date, "test");
        var result = new MarketDataRecoveryResult(request.OperationId, old.Role, old.Dataset, true, true, true, null, Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => OptionRecoveryReplacementProof.Validate(status, old, status, old, request, result));
        OptionRecoveryReplacementProof.Validate(status, old, status, old with { ProcessId = 200 }, request, result);
    }

    [Fact]
    public void Old_wire_shape_defaults_to_process_and_new_shape_keeps_all_old_fields()
    {
        // An eight-element array is the pre-capability status wire shape.
        // Guid uses the project's formatter, so take its bytes from an actual
        // serialization rather than inventing a different Guid representation.
        var original = new MarketDataBackendStatus(true, Date, [], Guid.NewGuid(), 17, [new byte[]{1,2}], EnabledOptionTrades:["1.1.1.1"]);
        var bytes = MessagePackSerializer.Serialize(original);
        Assert.Equal(0x99, bytes[0]); bytes[0] = 0x98;
        var oldBytes = bytes[..^1]; // default enum 0 occupies one byte
        Assert.Equal(MarketDataRecoveryOwnership.Process, MessagePackSerializer.Deserialize<MarketDataBackendStatus>(oldBytes).RecoveryOwnership);
        var native = original with { RecoveryOwnership = MarketDataRecoveryOwnership.TokioDatasetOwner };
        var nativeBytes=MessagePackSerializer.Serialize(native);
        Assert.Equal(native.RecoveryOwnership, MessagePackSerializer.Deserialize<MarketDataBackendStatus>(nativeBytes).RecoveryOwnership);
        var oldReader=MessagePackSerializer.Deserialize<LegacyBackendStatus>(nativeBytes);
        Assert.Equal(original.Ready,oldReader.Ready);Assert.Equal(original.ValueDate,oldReader.ValueDate);
        Assert.Equal(original.HostInstanceId,oldReader.HostInstanceId);Assert.Equal(original.Revision,oldReader.Revision);
        Assert.Equal(original.WorkerObservations![0],oldReader.WorkerObservations![0]);
        Assert.Equal(original.EnabledOptionTrades,oldReader.EnabledOptionTrades);
    }
    [MessagePackObject]
    public sealed record LegacyBackendStatus(
        [property:Key(0)]bool Ready,[property:Key(1)]DateOnly? ValueDate,
        [property:Key(2)]IReadOnlyList<MarketDataDatasetStatus> Datasets,
        [property:Key(3)]Guid HostInstanceId,[property:Key(4)]long Revision,
        [property:Key(5)]byte[][]? WorkerObservations,
        [property:Key(6)]TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingFailure? OptionProcessingFailure,
        [property:Key(7)]string[]? EnabledOptionTrades);

}
