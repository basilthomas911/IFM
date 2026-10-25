using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Rust.Bridge.Net;

internal static class ActorHealthFixtures
{
    public static async Task Export(string directory)
    {
        var now=new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc);
        var thread=new ActorHealthThreadId{ActorType=ActorType.Realtime,Name="IronCondorPosition",EntityId="101.701.1901.1501"};
        var snapshot=new ActorHealthSnapshot {
            ObservedUtc=now,OverallStatus=SupervisorActorHealthStatus.Yellow,ActorCount=1,RunningActorCount=1,QueuedMessageCount=2,
            SupervisorAuthorityState="Active",ManualMutationEnabled=true,
            Actors=[new(){ActorId=new(){ActorType=ActorType.Realtime,Name=thread.Name},Domain="Trade",Status=SupervisorActorHealthStatus.Yellow,
                Mailboxes=[new(){ThreadId=thread,QueueDepth=2,Generation=17,IsProcessing=true,LastAcceptedUtc=now.AddSeconds(-1),LifecycleState=ActorMailboxLifecycleState.Running},
                    new(){ThreadId=thread with{EntityId="other"},Generation=9,LastFailedUtc=now,LastCompletedUtc=now.AddMinutes(-1),LastError="Timeout"}]}],
            Incidents=[new(){ThreadId=thread,Generation=17,Health="Degraded",FirstObservedUtc=now.AddMinutes(-1),LastObservedUtc=now}],
            Workers=[new(){WorkerId=2,State=ActorThreadState.WaitingForMessage,IsRunning=true}],
            Projectors=[new(){ActorName="TradeEvent",ProjectorName="Position",IsReady=true,UpdatedUtc=now}],
            Operations=[new(){OperationId=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),ThreadId=thread,ExpectedGeneration=17,StartedUtc=now,Outcome="Succeeded"}],
            History=[new(){Revision=3,ObservedUtc=now,CriticalActors=0,DegradedActors=1}],
            Collection=new(){ObservedUtc=now,Revision=3,Gen2Collections=12,TotalGcPause=TimeSpan.FromTicks(123456)},Polling=new(){IsAlive=true,LastHeartbeatUtc=now,State="Running"}
        };
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};options.Converters.Add(new JsonStringEnumConverter());
        FixtureFiles.WriteAllText(Path.Combine(directory,"actor-health-http.json"),JsonSerializer.Serialize(snapshot,options));
        foreach(var enabled in new[]{false,true})
        {
            await using var session=new ActorHealthSession(new Query(snapshot),null!,new(){Enabled=enabled,Requester="Fixture operator"});
            var result=await session.ExecuteAsync(new(1,Guid.Parse("00000000-0000-4000-8000-000000000001"),"actor-health","load",0,null,new(){["fromUtc"]=now.AddHours(-1).ToString("O")}));
            FixtureFiles.WriteAllText(Path.Combine(directory,enabled?"actor-health-controlled-display.json":"actor-health-display.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        }
    }
    sealed class Query(ActorHealthSnapshot snapshot):IActorHealthQueryService
    {
        public Task<TomasAI.IFM.UI.Net.Services.Operations.UiOperationResult<ActorHealthSnapshot>> GetAsync(DateTime fromUtc,DateTime toUtc,CancellationToken cancellationToken=default)
            =>Task.FromResult(TomasAI.IFM.UI.Net.Services.Operations.UiOperationResult<ActorHealthSnapshot>.Success(snapshot));
    }
}

