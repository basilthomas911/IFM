using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.Recovery;
/// <summary>Executable Given/When/Then business acceptance scenarios.</summary>
public sealed class RecoveryAuditScenarios
{
    [Theory]
    [InlineData("ConnectionRestored",false)] [InlineData("ResetStarted",false)] [InlineData("Failed",false)] [InlineData("MonitoringRestored",true)]
    public void Given_recorded_fault_when_native_outcome_arrives_then_preserve_true_verification_scope(string kind,bool ready)
    {
        var incident=Guid.NewGuid();var state=new MarketDataFeedRecoveryState();var at=DateTimeOffset.UtcNow;
        var start=new RecoveryRecord {RecordId=Guid.NewGuid(),OperationId=incident,Sequence=1,Kind="Started",RuntimeId="native",Role="Options",Dataset="GLBX.MDP3",ValueDate=new(2026,10,29),StartedAtUtc=at,OccurredAtUtc=at,RecoveryDeadlineUtc=at.AddMinutes(2)};
        Append(start);
        if(kind=="MonitoringRestored")Append(start with {RecordId=Guid.NewGuid(),Sequence=2,Kind="Completed"});
        Append(start with {RecordId=Guid.NewGuid(),Sequence=kind=="MonitoringRestored"?3:2,Kind=kind,MonitoringReady=ready,CompletionScope=ready?"MonitoringReady":"TransportOnly"});
        Assert.Equal(kind=="MonitoringRestored"?3:2,state.Events.Count);Assert.Equal(incident,state.Latest!.OperationId);Assert.Equal(ready,state.Latest.MonitoringReady);Assert.Equal(start.StartedAtUtc,state.Latest.StartedAtUtc);
        void Append(RecoveryRecord record) {var payload=JsonSerializer.Serialize(record);state.Accept(new() {CommandId=Guid.NewGuid(),EntityId=new(incident),Subject=new(ActorType.Command,"MarketDataFeedRecoveryCommand","Record",incident.ToString()),Payload=payload,Digest=RecoveryRecord.Digest(payload)});}
    }
}
