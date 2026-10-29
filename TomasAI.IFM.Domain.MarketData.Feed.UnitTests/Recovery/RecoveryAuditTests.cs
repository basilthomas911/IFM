using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Recovery;
public sealed class RecoveryAuditTests
{
    [Fact] public void Optional_audit_health_preserves_old_backend_status_wire_format()
    {
        var original=new TomasAI.IFM.Application.MarketData.Contracts.MarketDataBackendStatus(true,new(2026,10,29),[]);
        var bytes=MessagePackSerializer.Serialize(original);
        // The previous contract has nine array fields. Remove only the new trailing nil.
        Assert.Equal(0x9a,bytes[0]);Assert.Equal(0xc0,bytes[^1]);
        var legacy=bytes[..^1];legacy[0]=0x99;
        var decoded=MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Contracts.MarketDataBackendStatus>(legacy);
        Assert.True(decoded.Ready);Assert.Equal(original.ValueDate,decoded.ValueDate);Assert.Null(decoded.RecoveryAudit);
        var health=new TomasAI.IFM.Application.MarketData.Contracts.MarketDataRecoveryAuditHealth(1,2,3,4,5,6,7,8,9,10,"audit unavailable");
        var current=MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Contracts.MarketDataBackendStatus>(MessagePackSerializer.Serialize(original with {RecoveryAudit=health}));
        Assert.Equal(health,current.RecoveryAudit);
        // A reader of the old array can skip the additive field without changing its first nine values.
        var reader=new MessagePackReader(MessagePackSerializer.Serialize(current));Assert.Equal(10,reader.ReadArrayHeader());
        for(var i=0;i<9;i++) reader.Skip();reader.Skip();Assert.True(reader.End);
    }
    static RecoveryRecord Started() => new() { RecordId=Guid.NewGuid(),OperationId=Guid.NewGuid(),Sequence=1,Kind="Started",RuntimeId="runtime",Role="Options",Dataset="GLBX.MDP3",ValueDate=new(2026,10,29),
        StartedAtUtc=new(2026,10,29,0,0,0,TimeSpan.Zero),OccurredAtUtc=new(2026,10,29,0,0,0,TimeSpan.Zero),RecoveryDeadlineUtc=new(2026,10,29,0,2,0,TimeSpan.Zero),MissingProofs=["Transport","Pricing"] };
    static AppendMarketDataFeedRecoveryCommand Command(RecoveryRecord record)
    {
        var json=JsonSerializer.Serialize(record);var id=new MarketDataFeedRecoveryId(record.OperationId);
        return new() { CommandId=Guid.NewGuid(),EntityId=id,Subject=new(ActorType.Command,AppendMarketDataFeedRecoveryCommand.Actor,"Record",id.Format()),Payload=json,Digest=RecoveryRecord.Digest(json),
            AttemptId=Guid.NewGuid(),AttemptStartedAtUtc=DateTime.UtcNow,RequestDeadlineUtc=DateTime.UtcNow.AddMinutes(2) };
    }
    [Fact] public void Frozen_CLR_and_stable_event_names_resolve_to_the_same_contract()
    {
        var type=typeof(MarketDataFeedRecoveryStartedEvent);
        var identity=EventWriteIdentityRegistry.Default.GetIdentity(type);
        Assert.Equal(type.Name,identity.EventName);
        Assert.Equal(type,EventTypeRegistry.Default.Resolve(identity.EventTypeName));
        Assert.Equal(type,EventTypeRegistry.Default.Resolve("marketdata.recovery.marketdatafeedrecoverystartedevent/v1"));
        Assert.Contains("TomasAI.IFM.Application.Contracts.MarketData.Feed, Version=1.0.0.0",identity.EventTypeName);
    }
    [Fact] public void Six_hundred_seven_retries_produce_one_source_fact_with_original_time()
    {
        var record=Started();var state=new MarketDataFeedRecoveryState();var command=Command(record);Assert.True(state.Accept(command));
        for(var i=0;i<607;i++) Assert.False(state.Accept(command with { CommandId=Guid.NewGuid(),AttemptId=Guid.NewGuid() }));
        Assert.Single(state.Events);Assert.Equal(record.OccurredAtUtc,state.Latest!.OccurredAtUtc);Assert.IsType<MarketDataFeedRecoveryStartedEvent>(state.Events.Single());
    }
    [Fact] public void Same_record_different_digest_is_rejected_without_mutation()
    {
        var record=Started();var state=new MarketDataFeedRecoveryState();state.Accept(Command(record));
        Assert.Throws<InvalidDataException>(()=>state.Accept(Command(record with { LatestError="changed" })));
        Assert.Single(state.Events);
    }
    [Fact] public void Missing_predecessor_and_conflicting_sequence_do_not_fabricate_start()
    {
        var record=Started();var state=new MarketDataFeedRecoveryState();Assert.Throws<InvalidDataException>(()=>state.Accept(Command(record with {Sequence=2,Kind="Failed"})));Assert.Empty(state.Events);
        state.Accept(Command(record));Assert.Throws<InvalidDataException>(()=>state.Accept(Command(record with { RecordId=Guid.NewGuid() })));Assert.Single(state.Events);
    }
    [Fact] public void Snapshot_plus_tail_reconstructs_exact_incident_and_duplicate_index()
    {
        var record=Started();var state=new MarketDataFeedRecoveryState();state.Accept(Command(record));
        var next=record with {RecordId=Guid.NewGuid(),Sequence=2,Kind="ConnectionRestored",ProviderActivityVerified=true};state.Accept(Command(next));
        var restored=new MarketDataFeedRecoveryState();restored.ReplayEvents(state.Events);
        Assert.Equal(JsonSerializer.Serialize(next),JsonSerializer.Serialize(restored.Latest));Assert.False(restored.Accept(Command(record)));Assert.False(restored.Accept(Command(next)));
    }
    [Fact] public void Independent_record_functions_do_not_suppress_later_incident_transitions()
    {
        var record=Started();var first=new MarketDataFeedRecoveryRecordState();var second=new MarketDataFeedRecoveryRecordState();
        var command=Command(record);var request=new RecordMarketDataFeedRecoveryCommand {EntityId=new(record.RecordId),Digest=command.Digest};
        first.TryComplete(new() {RecordId=record.RecordId,Digest=command.Digest},request);
        Assert.True(first.Matches(request with {CommandId=Guid.NewGuid(),AttemptId=Guid.NewGuid(),RequestDeadlineUtc=DateTime.UtcNow.AddDays(1)}));Assert.False(second.IsCompleted);
        Assert.False(first.Matches(request with {Digest="different"}));
    }
    [Theory] [InlineData("ResetStarted")] [InlineData("ConnectionRestored")] [InlineData("Completed")] [InlineData("Failed")] [InlineData("Interrupted")]
    public void Source_event_roundtrip_preserves_exact_payload_and_identity(string kind)
    {
        var record=Started();var state=new MarketDataFeedRecoveryState();state.Accept(Command(record));state.Accept(Command(record with {RecordId=Guid.NewGuid(),Sequence=2,Kind=kind}));
        var source=(MarketDataFeedRecoveryFact)state.Events.Last();var bytes=MessagePackSerializer.Serialize(source.GetType(),source);
        var decoded=(MarketDataFeedRecoveryFact)MessagePackSerializer.Deserialize(source.GetType(),bytes)!;Assert.Equal(source,decoded);
    }
    [Fact] public void Quiet_transport_completion_cannot_claim_full_monitoring()
    {
        var record=Started() with { MonitoringReady=true,CompletionScope="TransportOnly" };var command=Command(record);
        Assert.Throws<InvalidDataException>(()=>RecoveryRecord.Parse(command.Payload,command.Digest));
    }
    [Fact] public void Reset_completion_requires_actual_retirement_and_replacement()
    {
        var record=Started();var state=new MarketDataFeedRecoveryState();state.Accept(Command(record));
        Assert.Throws<InvalidDataException>(()=>state.Accept(Command(record with {RecordId=Guid.NewGuid(),Sequence=2,Kind="ResetCompleted"})));
        Assert.Single(state.Events);
    }
    [Fact] public void Actual_reset_timeline_replays_without_losing_owner_or_monitoring_evidence()
    {
        var first=Started() with {OldOwner="old-worker/old-generation"};var state=new MarketDataFeedRecoveryState();
        Assert.True(state.Accept(Command(first)));
        Assert.True(state.Accept(Command(first with {RecordId=Guid.NewGuid(),Sequence=2,Kind="ResetStarted"})));
        var restored=first with {NewOwner="new-worker/new-generation",OldResourcesRetired=true,MissingProofs=[]};
        Assert.True(state.Accept(Command(restored with {RecordId=Guid.NewGuid(),Sequence=3,Kind="ResetCompleted"})));
        Assert.True(state.Accept(Command(restored with {RecordId=Guid.NewGuid(),Sequence=4,Kind="ConnectionRestored",ProviderActivityVerified=true})));
        Assert.True(state.Accept(Command(restored with {RecordId=Guid.NewGuid(),Sequence=5,Kind="Completed",ProviderActivityVerified=true,MonitoringReady=true,CompletionScope="MonitoringReady"})));
        Assert.IsType<MarketDataFeedRecoveryStartedEvent>(state.Events.First());
        var replay=new MarketDataFeedRecoveryState();replay.ReplayEvents(state.Events);
        Assert.Equal(5,replay.Latest!.Sequence);Assert.True(replay.Latest.MonitoringReady);
        Assert.Equal("new-worker/new-generation",replay.Latest.NewOwner);
    }
    [Fact] public void Audit_historical_value_date_is_not_replaced_by_current_api_date()
    {
        var record=Started() with {ValueDate=new(2020,1,2)};var state=new MarketDataFeedRecoveryState();Assert.True(state.Accept(Command(record)));Assert.Equal(new DateOnly(2020,1,2),state.Latest!.ValueDate);
    }
    [Theory] [InlineData("Failed")] [InlineData("Interrupted")]
    public void Terminal_incident_cannot_be_reopened_by_late_completion(string kind)
    {
        var record=Started();var state=new MarketDataFeedRecoveryState();state.Accept(Command(record));state.Accept(Command(record with {RecordId=Guid.NewGuid(),Sequence=2,Kind=kind}));
        Assert.Throws<InvalidDataException>(()=>state.Accept(Command(record with {RecordId=Guid.NewGuid(),Sequence=3,Kind="Completed"})));
    }
    [Fact] public void Tampered_or_oversized_payload_is_rejected_before_event_creation()
    {
        var record=Started();var command=Command(record);Assert.Throws<InvalidDataException>(()=>RecoveryRecord.Parse(command.Payload+" ",command.Digest));
        Assert.Throws<InvalidDataException>(()=>RecoveryRecord.Parse(new string('x',262145),"bad"));
    }
}
