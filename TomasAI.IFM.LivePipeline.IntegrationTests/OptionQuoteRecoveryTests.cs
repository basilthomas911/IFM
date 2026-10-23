using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionQuoteRecoveryTests
{
    [Fact]
    public void Only_quote_router_is_allowed_and_replacement_owns_new_routes_and_generation()
    {
        Assert.True(RealtimeActorResetPolicy.CanReplace(typeof(FuturesOptionRealtimeActor),ActorType.Realtime));
        Assert.True(RealtimeActorResetPolicy.CanReplace(typeof(TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor.IronCondorTradePositionRealtimeActor), ActorType.Realtime));
        Assert.False(RealtimeActorResetPolicy.CanReplace(typeof(TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor.OrderExecutionCommandActor),ActorType.Realtime));
        var supervisor = Substitute.For<IActorSupervisor>();
        var db = Substitute.For<IDbContextFactory>();
        var service = Substitute.For<IActorService>();
        var logger = Substitute.For<ILogger<FuturesOptionRealtimeActor>>();
        var cache = new TradeRealtimeCacheRegistry();
        var original = new FuturesOptionRealtimeContext(supervisor,db,service,logger,cache);
        var dependencies = new Dictionary<Type,object> {
            [typeof(IRealtimeActorContext<FuturesOptionRealtimeActor>)] = original,
            [typeof(IActorSupervisor)] = supervisor,[typeof(IDbContextFactory)] = db,
            [typeof(IActorService)] = service,[typeof(ILogger<FuturesOptionRealtimeActor>)] = logger,
            [typeof(TradeRealtimeCacheRegistry)] = cache };
        var replacement = (FuturesOptionRealtimeActor)RealtimeActorReplacementFactory.Create(typeof(FuturesOptionRealtimeActor),t=>dependencies[t]);
        Assert.NotSame(original.RealtimeGeneration,replacement.RealtimeGeneration);
        original.RealtimeGeneration.Retire();
        Assert.False(replacement.RealtimeGeneration.Token.IsCancellationRequested);
        Assert.ThrowsAny<OperationCanceledException>(() => original.RealtimeGeneration.Enter());
    }

    [Fact]
    public async Task Retired_router_cannot_mutate_shared_cache_or_dispatch_a_position_command()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var service = new ActorService(supervisor);
        var generation = new RealtimeActorGeneration();
        using var execution = generation.Enter();
        generation.Retire();
        var registry = new TradeRealtimeCacheRegistry();
        Assert.ThrowsAny<OperationCanceledException>(() => registry.Expire());
        var position = TomasAI.IFM.Domain.Trade.Shared.StrategyPositionId.Create(new(1,2,3,4),
            TomasAI.IFM.Domain.Trade.Shared.TradeStrategyKind.IronCondor);
        var command = new TomasAI.IFM.Domain.Trade.Shared.Trade.Position.ChangeTradeLegDataCommand {
            EntityId = position, CommandId = Guid.NewGuid() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SendAsync<
            TomasAI.IFM.Domain.Trade.Shared.Trade.Position.ChangeTradeLegDataCommand,
            TomasAI.IFM.Domain.Trade.Shared.StrategyPositionId>(command,position).AsTask());
        Assert.Empty(supervisor.ReceivedCalls());
    }

    [Fact]
    public void Repeated_position_failures_are_unhealthy_even_when_mailbox_admission_is_open()
    {
        var now = DateTime.UtcNow;
        var progress = new TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model.OptionQuoteRoutingProgress();
        progress.Record("ES-call",false,now.AddMinutes(-1));
        progress.Record("ES-put",true,now);
        var metrics = new ActorMetricsSnapshot(new(ActorType.Realtime,"FuturesOptionRealtime"),now,[],1);
        var result = OptionQuoteProcessingHealth.Evaluate(metrics,true,true,now,progress.IsHealthy(now));
        Assert.Equal("Degraded",result.Status);
        progress.Record("ES-call",true,now);
        Assert.True(progress.IsHealthy(now));
        Assert.Equal("Healthy",OptionQuoteProcessingHealth.Evaluate(metrics,true,true,now,progress.IsHealthy(now)).Status);
    }

    [Theory]
    [InlineData(false,0, false)]
    [InlineData(true,32,false)]
    [InlineData(true,0,true)]
    public void Closed_full_and_stalled_mailboxes_are_required_failures(bool open,int depth,bool stalled)
    {
        var now = DateTime.UtcNow;
        var mailbox = new ActorMailboxMetricsSnapshot(new ActorSubject(ActorType.Realtime,"FuturesOptionRealtime","Changed","ES").ThreadId,
            depth,32,depth,0,0,0,0,0,0,0,open,open?ActorMailboxLifecycleState.Running:ActorMailboxLifecycleState.Quarantined,
            1,stalled,"Changed",now,now.AddMinutes(-2),now.AddMinutes(-2),null,"","",1);
        var result=OptionQuoteProcessingHealth.Evaluate(new(new(ActorType.Realtime,"FuturesOptionRealtime"),now,[mailbox],1),true,true,now);
        Assert.True(result.Required);
        Assert.Equal("Degraded",result.Status);
    }
}
