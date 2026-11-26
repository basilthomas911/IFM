using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
using System.Security.Cryptography;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Time;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class RecordedMonitoringGenerationTests
{
    [Fact]
    public void Exact_original_permission_can_hydrate_missing_cache_but_never_replace_a_newer_position()
    {
        using var requests=new IronCondorPositionRequests();var clock=new RecordedClock();
        var context=new IronCondorTradePositionRealtimeContext(Substitute.For<IActorSupervisor>(),NullLogger<IronCondorTradePositionRealtimeActor>.Instance,requests:requests,timeProvider:clock);
        var input=Input();var hash=Hash(input);clock.Expected=Proof(input,hash);
        Assert.False(context.IsCurrentMonitoringPosition(input.PositionSnapshot,input.MonitoringGenerationId));
        Assert.False(context.IsCurrentRecordedMonitoringInput(input,hash+"00"));
        Assert.True(context.IsCurrentRecordedMonitoringInput(input,hash));
        Assert.True(context.IsCurrentMonitoringPosition(input.PositionSnapshot,context.RealtimeGeneration.Id));
        var next=input.PositionSnapshot with{PositionSequence=input.PositionSnapshot.PositionSequence+1};
        context.CaptureMonitoringInputs(next,input.ValueDate,input.ReceivedOn);
        Assert.False(context.IsCurrentRecordedMonitoringInput(input,hash));
        Assert.True(context.IsCurrentMonitoringPosition(next,context.RealtimeGeneration.Id));
        context.RealtimeGeneration.Retire();Assert.False(context.IsCurrentRecordedMonitoringInput(input,hash));
    }
    [Fact]
    public async Task Only_original_current_generation_emits_exact_permission_evidence_before_calculation()
    {
        using var requests=new IronCondorPositionRequests();var recorder=new Recorder();
        var context=new IronCondorTradePositionRealtimeContext(Substitute.For<IActorSupervisor>(),NullLogger<IronCondorTradePositionRealtimeActor>.Instance,requests:requests,sessionRecorder:recorder);
        var old=Input();context.CaptureMonitoringInputs(old.PositionSnapshot,old.ValueDate,old.ReceivedOn);
        await Assert.ThrowsAsync<InvalidDataException>(()=>context.RecordMonitoringGenerationAcceptanceAsync(old,Hash(old)).AsTask());Assert.Empty(recorder.Records);
        var current=old with{MonitoringGenerationId=context.RealtimeGeneration.Id};
        await context.RecordMonitoringGenerationAcceptanceAsync(current,Hash(current));
        var captured=Assert.Single(recorder.Records);Assert.Equal("MonitoringGenerationAccepted",captured.Kind);Assert.Empty(captured.Destinations);
        Assert.Equal(Proof(current,Hash(current)),MessagePackSerializer.Deserialize<RecordedRealtimeGenerationAcceptance>(captured.OriginalWire));
        Assert.Equal(current.ReceivedOn,old.ReceivedOn); // original source clock is unchanged
    }
    static IronCondorMonitoringInputsChangedEvent Input()
    {
        var id=StrategyPositionId.Create(new(1,2,3,4),TradeStrategyKind.IronCondor);
        return new(){Id=Guid.NewGuid(),EntityId=id,Subject=new(ActorType.Realtime,IronCondorTradePositionRealtimeActor.ActorName,IronCondorMonitoringInputsChangedEvent.Verb,id.Format()),
            MonitoringGenerationId=Guid.NewGuid(),ValueDate=new(2026,11,20),ReceivedOn=new(2026,11,20,15,0,0,DateTimeKind.Utc),
            PositionSnapshot=new(){Id=id,PositionSequence=5,RouteGeneration=2,IsOpen=true,StrategyKind=TradeStrategyKind.IronCondor}};
    }
    static string Hash(IronCondorMonitoringInputsChangedEvent input)=>Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(input)));
    static RecordedRealtimeGenerationAcceptance Proof(IronCondorMonitoringInputsChangedEvent input,string hash)=>new(){Subject=input.Subject,EventId=input.Id,OriginalGenerationId=input.MonitoringGenerationId,PayloadSha256=hash,PositionSequence=input.PositionSnapshot.PositionSequence,RouteGeneration=input.PositionSnapshot.RouteGeneration};
    sealed class RecordedClock:TimeProvider,IRecordedRealtimeSourceReplay,IRecordedRealtimeGenerationAuthority
    {
        public RecordedRealtimeGenerationAcceptance? Expected;
        public bool UsesRecordedSource(string actor,string verb)=>actor==IronCondorTradePositionRealtimeActor.ActorName&&verb==IronCondorMonitoringInputsChangedEvent.Verb;
        public bool Allows(RecordedRealtimeGenerationAcceptance original,Guid currentGenerationId)=>currentGenerationId!=Guid.Empty&&original==Expected;
    }
    sealed class Recorder:IRealtimeSessionRecorder
    {
        public bool Enabled=>true;public List<RealtimeSessionArchiveRecord> Records {get;}=[];
        public ValueTask<RealtimeSessionArchiveReceipt> RecordAsync(RealtimeSessionArchiveRecord record,CancellationToken token=default)
        {Records.Add(record);return ValueTask.FromResult(new RealtimeSessionArchiveReceipt(Convert.ToHexString(SHA256.HashData(record.OriginalWire.Span)),"receipt",false,"manifest"));}
    }
}
