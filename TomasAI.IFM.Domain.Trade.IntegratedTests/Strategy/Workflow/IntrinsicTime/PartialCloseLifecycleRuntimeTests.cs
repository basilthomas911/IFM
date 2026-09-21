using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Theory,Trait("Category","EventLogPartialCloseLifecycle")]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Partial_close_actors_persist_remaining_quantity_and_complete_after_host_restart(int direction)
    {
        EventLogEngineQualification.Validate().Should().NotBeNull();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var owner=Random.Shared.Next(100000,900000000);
        var id=new TradeEntityId(owner,owner+1,1,1);
        var position=StrategyPositionId.Create(id,TradeStrategyKind.FuturesOutright);
        var now=DateTime.UtcNow;
        var component=Guid.NewGuid(); var attempt=Guid.NewGuid(); var leg=Guid.NewGuid();
        var trade=new EstablishedTradeDefinition
        {
            Id=id,AssetFamily=TradeAssetFamily.Futures,StrategyKind=TradeStrategyKind.FuturesOutright,
            SourceComponentId=component,ExecutionAttemptId=attempt,Status=EstablishedTradeStatus.Open,
            EstablishedAtUtc=now,EvidenceRevision=1,
            Legs=[new() { TradeLegId=leg,ContractId="ES-SYNTHETIC-LIFECYCLE",SignedQuantity=direction*3,CashMultiplier=50 }],
            OriginalFills=[new() { ExecutionFillId=Guid.NewGuid(),ExecutionAttemptId=attempt,ComponentId=component,
                TradeLegId=leg,ContractId="ES-SYNTHETIC-LIFECYCLE",SignedQuantity=direction*2,Price=100,FilledAtUtc=now,
                ExternalExecutionId=$"OPEN-{attempt:N}" }]
        };
        CloseFuturesTradeCommand CloseTrade(int ordinal)
        {
            var closeAttempt=Guid.NewGuid();
            return new() { CommandId=Guid.NewGuid(),EntityId=id,
                Subject=new(ActorType.Command,"FuturesTradeCommand",CloseFuturesTradeCommand.Verb,id.Format()),
                ClosedAtUtc=now.AddSeconds(ordinal),ClosingFills=[trade.OriginalFills[0] with
                { ExecutionFillId=Guid.NewGuid(),ExecutionAttemptId=closeAttempt,SignedQuantity=-direction,Price=110,
                    FilledAtUtc=now.AddSeconds(ordinal),ExternalExecutionId=$"CLOSE-{closeAttempt:N}" }] };
        }
        async Task Apply(IActorService actors,CloseFuturesTradeCommand close)
        {
            var begin=new BeginCloseFuturesTradeCommand { CommandId=Guid.NewGuid(),EntityId=id,
                Subject=new(ActorType.Command,"FuturesTradeCommand",BeginCloseFuturesTradeCommand.Verb,id.Format()) };
            var began=await actors.SendAsync<BeginCloseFuturesTradeCommand,TradeEntityId>(begin,id,deadline.Token);
            began.Success.Should().BeTrue(began.ErrorMessage);
            var applied=await actors.SendAsync<CloseFuturesTradeCommand,TradeEntityId>(close,id,deadline.Token);
            applied.Success.Should().BeTrue(applied.ErrorMessage);
            var reduce=new CloseFuturesPositionCommand { CommandId=Guid.NewGuid(),EntityId=position,
                Subject=new(ActorType.Command,FuturesPositionActorNames.Command,CloseFuturesPositionCommand.Verb,position.Format()),
                EffectiveAtUtc=close.ClosedAtUtc,ClosingFills=close.ClosingFills };
            var reduced=await actors.SendAsync<CloseFuturesPositionCommand,StrategyPositionId>(reduce,position,deadline.Token);
            reduced.Success.Should().BeTrue(reduced.ErrorMessage);
            (await actors.SendAsync<CloseFuturesPositionCommand,StrategyPositionId>(reduce,position,deadline.Token)).Success.Should().BeTrue();
        }
        async Task<StrategyPositionSnapshot> WaitFor(int remaining,int fillCount)
        {
            while(true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var snapshot=await database.DbFactory.TradeDb.GetStrategyPositionAsync(position,deadline.Token);
                var saved=await database.DbFactory.TradeDb.GetEstablishedTradeAsync(id,deadline.Token);
                if(snapshot is not null && snapshot.Legs.Single().SignedQuantity==remaining && snapshot.ClosingFills.Length==fillCount &&
                    saved is not null && saved.ClosingFills.Length==fillCount &&
                    saved.Status==(remaining==0?EstablishedTradeStatus.Closed:EstablishedTradeStatus.Open)) return snapshot;
                await Task.Delay(25,deadline.Token);
            }
        }
        var partial=CloseTrade(1);
        await using(var host=Host(brokerUrl:Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL"),actualPortfolio:true))
        {
            _=host.CreateClient(); var supervisor=host.Services.GetRequiredService<IActorSupervisor>();
            try
            {
                var actors=host.Services.GetRequiredService<IActorService>();
                var create=new CreateFuturesTradeCommand { CommandId=Guid.NewGuid(),EntityId=id,Trade=trade,
                    Subject=new(ActorType.Command,"FuturesTradeCommand",CreateFuturesTradeCommand.Verb,id.Format()) };
                var created=await actors.SendAsync<CreateFuturesTradeCommand,TradeEntityId>(create,id,deadline.Token);
                created.Success.Should().BeTrue(created.ErrorMessage);
                await WaitFor(direction*2,0);
                await Apply(actors,partial);
                (await WaitFor(direction,1)).IsOpen.Should().BeTrue();
            }
            finally { await supervisor.ShutdownAsync(); }
        }
        await using(var host=Host(brokerUrl:Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL"),actualPortfolio:true))
        {
            _=host.CreateClient();var supervisor=host.Services.GetRequiredService<IActorSupervisor>();
            try
            {
                var actors=host.Services.GetRequiredService<IActorService>();
                (await actors.SendAsync<CloseFuturesTradeCommand,TradeEntityId>(partial,id,deadline.Token)).Success.Should().BeTrue();
                await Apply(actors,CloseTrade(2));
                var closed=await WaitFor(0,2);
                closed.IsOpen.Should().BeFalse();closed.RealizedPnl.Should().Be(direction*20);
            }
            finally { await supervisor.ShutdownAsync(); }
        }
    }
}
