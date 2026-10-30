using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Serialization;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresTradeFlowTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Position_history_persists_currency_without_requiring_a_trade_plan()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaSql.Create, [], ct));
        var id = new TradeEntityId(Random.Shared.Next(60_000_000, 70_000_000), 1, 1, 1);
        var at = new DateTime(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc);
        var leg = Guid.NewGuid();
        var trade = new EstablishedTradeDefinition { Id = id, EstablishedAtUtc = at, OpeningCommission = 2.60m,
            Legs = [new() { TradeLegId = leg, ContractId = "currency-test", CashMultiplier = 50, SignedQuantity = -2 }] };
        var position = new StrategyPositionSnapshot { Id = new(id, Guid.NewGuid()), ValueDate = new(2026, 10, 16),
            Phase = StrategyPositionPhase.EndOfDay, PositionSequence = 1, RouteGeneration = 1, AsOfUtc = at,
            DailyPnlPoints = .60m, Legs = [new() { TradeLegId = leg, ContractId = "currency-test", SignedQuantity = -2 }] };
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ExecuteAsync("INSERT INTO trade.established_trade(portfolioid,fundid,orderid,tradeid,payload) VALUES($1,$2,$3,$4,$5);",
                [id.PortfolioId,id.FundId,id.OrderId,id.TradeId,MessagePackBinarySerializer.Shared.Serialize(trade)], ct);
            await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db, position, ct);
            return true;
        });
        var payload = await fixture.Transactions.ExecuteAsync((db, ct) => db.ScalarAsync(
            "SELECT payload FROM trade.strategy_position_history WHERE positionid=$1;", [position.Id.PositionId], ct));
        var stored = MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])payload!);
        stored.DailyPnl.Should().Be(27.40m);
        stored.DailyPnlPoints.Should().Be(.60m);
        stored.ContractCashMultiplier.Should().Be(50m);
        StrategyPositionCurrencyPnl.Apply(stored, trade).DailyPnl.Should().Be(27.40m);
    }

    [Fact]
    public async Task Order_lifecycle_advances_within_one_definition_revision_and_conflicting_terms_roll_back()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaSql.Create, [], ct));
        var id = new TradeOrderId(Random.Shared.Next(50_000_000, 60_000_000), 1, 1);
        var accepted = DateTime.UtcNow;
        var order = new TradeOrderDefinition { Id = id, Revision = 1, DefinitionHash = "immutable-terms" };
        var stream = "lifecycle-projection-" + Guid.NewGuid().ToString("N");
        long version = 0;
        async Task Save(TradeOrderDefinition value) => await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            var commandId = Guid.NewGuid();
            await db.AppendAsync(stream, commandId, new TradeOrderChangedEvent
            { Id = Guid.NewGuid(), EntityId = id, CommandId = commandId, TradeOrderDefinition = value, ReceivedOn = accepted }, version, ct);
            await db.TradeFlowProjectOrderLifecycleAsync(value, accepted, ct);
            return true;
        });
        foreach (var status in new[] { TradeOrderStatus.Draft, TradeOrderStatus.Approved, TradeOrderStatus.Ready, TradeOrderStatus.Executing })
        {
            order = order with { Status = status };
            if (status == TradeOrderStatus.Executing)
                order = order with { BoundExecutionAttemptId = Guid.NewGuid(), BoundExecutionChannel = ExecutionChannel.Broker, ExecutionBoundAtUtc = accepted };
            await Save(order);
            version++;
        }
        var conflict = order with { DefinitionHash = "changed-terms" };
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => Save(conflict));
        (await fixture.EventCountAsync(stream)).Should().Be(4);
        var payload = await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ScalarAsync(
            "SELECT payload FROM trade.trade_order WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND revision=1;",
            [id.PortfolioId, id.FundId, id.OrderId], ct));
        MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>((byte[])payload!).Should().BeEquivalentTo(order);
    }

    [Fact]
    public async Task EOD_retry_can_resume_matching_orphan_audit_but_not_change_payload_or_repeat_committed_events()
    {
        var id = new StrategyPositionId(new(999, 1, 1, 1), Guid.NewGuid());
        var boundary = DateTime.UtcNow;
        var command = new EndOfDayFuturesPositionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, ValueDate = DateOnly.FromDateTime(boundary), EffectiveAtUtc = boundary,
            Subject = new(ActorType.Command, FuturesPositionActorNames.Command, EndOfDayFuturesPositionCommand.Verb, id.Format())
        };
        // Reproduce the old separate audit reservation without committing a source fact.
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) => { await db.ReserveCommandAsync(command, ct); return true; });
        var conflict = () => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        { await db.ReserveCommandAsync(command with { EffectiveAtUtc = boundary.AddSeconds(1) }, ct); return true; });
        await FluentActions.Awaiting(conflict).Should().ThrowAsync<CommandAuditPayloadConflictException>();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(0);
        var source = new FuturesPositionChangedEvent
        {
            Id = Guid.NewGuid(), EntityId = id, CommandId = command.CommandId, ReceivedOn = boundary,
            PositionSnapshot = new() { Id = id, StrategyKind = TradeStrategyKind.FuturesOutright, ValueDate = command.ValueDate,
                LatestSealedValueDate = command.ValueDate, CloseBoundaryUtc = boundary, PositionSequence = 1, Phase = StrategyPositionPhase.EndOfDay }
        };
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ReserveCommandAsync(command, ct);
            await db.AppendAsync(command.StreamId, command.CommandId, source, 0, ct);
            return true;
        });
        var duplicate = () => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        { await db.ReserveCommandAsync(command, ct); return true; });
        await FluentActions.Awaiting(duplicate).Should().ThrowAsync<CommandAuditDuplicateException>();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Order_and_source_commit_together_or_rollback_together(bool injectFailure)
    {
        await fixture.Transactions.ExecuteAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var id=new TradeOrderId(Random.Shared.Next(10_000_000,20_000_000),1,1);
        var acceptedAt=DateTime.UtcNow;
        var order=new TradeOrderDefinition { Id=id,Revision=1,Status=TradeOrderStatus.Draft,ValueDate=DateOnly.FromDateTime(acceptedAt),ValidUntilUtc=acceptedAt.AddMinutes(5) };
        var source=new TradeOrderChangedEvent { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=id,TradeOrderDefinition=order,ReceivedOn=acceptedAt };
        var stream="native-trade-order-test-"+source.Id;

        async Task Write()=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.AppendAsync(stream,source.CommandId,source,0,ct);
            await TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,order,source.ReceivedOn,ct);
            if(injectFailure) throw new InvalidOperationException("Injected after projection before commit.");
            return true;
        });
        if(injectFailure) await FluentActions.Awaiting(Write).Should().ThrowAsync<InvalidOperationException>();
        else await Write();
        (await fixture.EventCountAsync(stream)).Should().Be(injectFailure?0:1);
        var payload=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT payload FROM trade.trade_order WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND revision=$4;",[id.PortfolioId,id.FundId,id.OrderId,1],ct));
        if(injectFailure) payload.Should().BeNull();
        else
        {
            MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>((byte[])payload!).Should().BeEquivalentTo(order);
            var first=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT to_jsonb(t)::text FROM trade.trade_order t WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND revision=$4;",[id.PortfolioId,id.FundId,id.OrderId,1],ct));
            await fixture.Transactions.ExecuteAsync((db,ct)=>TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,source.TradeOrderDefinition,source.ReceivedOn,ct));
            var repeated=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT to_jsonb(t)::text FROM trade.trade_order t WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND revision=$4;",[id.PortfolioId,id.FundId,id.OrderId,1],ct));
            repeated.Should().Be(first);
            await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,order with { Origin="Conflicting same revision" },source.ReceivedOn,ct)))
                .Should().ThrowAsync<System.IO.InvalidDataException>();
        }
    }

    [Fact]
    public async Task Established_trade_revisions_are_immutable_and_stale_history_cannot_replace_current_state()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var trade = new EstablishedTradeDefinition
        {
            Id = new TradeEntityId(Random.Shared.Next(60_000_000,70_000_000),1,1,1),
            StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = DateTime.UtcNow,
            EvidenceRevision = 1,
            OpeningValue = 100
        };

        async Task Write(EstablishedTradeDefinition value) => await fixture.Transactions.ExecuteOnceAsync(async (db,ct) =>
        {
            await TradeDbContextExtensions.TradeFlowProjectTradeAsync(db,value,ct);
            return true;
        });
        async Task<EstablishedTradeDefinition> Read() => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(
            (byte[])(await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync(
                "SELECT payload FROM trade.established_trade WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4;",
                [trade.Id.PortfolioId,trade.Id.FundId,trade.Id.OrderId,trade.Id.TradeId],ct)))!);
        await Write(trade);
        await Write(trade);
        await FluentActions.Awaiting(()=>Write(trade with { OpeningValue=101 })).Should().ThrowAsync<System.IO.InvalidDataException>();
        (await Read()).Should().BeEquivalentTo(trade);
        // Even a changed history-key timestamp cannot admit different facts for the same current revision.
        await FluentActions.Awaiting(()=>Write(trade with { EstablishedAtUtc=trade.EstablishedAtUtc.AddSeconds(1) }))
            .Should().ThrowAsync<System.IO.InvalidDataException>();
        var later=trade with { EvidenceRevision=2, OpeningValue=110 };
        await Write(later);
        await Write(trade);
        (await Read()).Should().BeEquivalentTo(later);
    }

    [Fact]
    public async Task Position_daily_history_routes_and_close_are_atomic_and_stale_marks_cannot_restore_routes()
    {
        await fixture.Transactions.ExecuteAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var now=DateTime.UtcNow;var id=new StrategyPositionId(new TradeEntityId(Random.Shared.Next(20_000_000,30_000_000),1,1,1),Guid.NewGuid());

        var opening=new StrategyPositionSnapshot { Id=id,StrategyKind=TradeStrategyKind.IronCondor,Phase=StrategyPositionPhase.Open,
            PositionSequence=1,RouteGeneration=1,AsOfUtc=now,ValueDate=DateOnly.FromDateTime(now),IsOpen=true,
            Legs=[new StrategyPositionLeg { TradeLegId=Guid.NewGuid(),ContractId="native-route-"+id.PositionId,SignedQuantity=1 }] };
        async Task Write(StrategyPositionSnapshot position)=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=> { await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db,position,ct); return true; });
        async Task<long> Count(string table)=>Convert.ToInt64(await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM trade."+table+" WHERE positionId=$1;",[id.PositionId],ct)));
        await Write(opening);
        var daily=opening with { PositionSequence=2,Phase=StrategyPositionPhase.MarkToMarket,AsOfUtc=now.AddSeconds(1),HistoryAsOfUtc=now.AddSeconds(1),HistoryPositionSequence=2,DailyPnl=-10 };
        await Write(daily);await Write(daily with {PositionSequence=3,AsOfUtc=now.AddSeconds(2),DailyPnl=-20});
        var eod=daily with {PositionSequence=4,Phase=StrategyPositionPhase.EndOfDay,AsOfUtc=now.AddSeconds(3),DailyPnl=-25};
        await Write(eod);(await Count("strategy_position_history")).Should().Be(2);
        var row=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT payload FROM trade.strategy_position_history WHERE positionId=$1 AND positionSequence=2;",[id.PositionId],ct));
        MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])row!).Should().BeEquivalentTo(eod);
        var closed=eod with {PositionSequence=5,Phase=StrategyPositionPhase.Close,AsOfUtc=now.AddSeconds(4),HistoryAsOfUtc=now.AddSeconds(4),HistoryPositionSequence=5,IsOpen=false};
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=> { await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db,closed,ct);throw new InvalidOperationException("Close rollback"); })).Should().ThrowAsync<InvalidOperationException>();
        (await Count("open_position_route")).Should().Be(1);(await Count("open_position_route_recovery")).Should().Be(1);
        await Write(closed);await Write(daily);await Write(closed);
        (await Count("open_position_route")).Should().Be(0);(await Count("open_position_route_recovery")).Should().Be(0);(await Count("strategy_position_history")).Should().Be(3);
        var current=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT payload FROM trade.strategy_position_current WHERE positionId=$1;",[id.PositionId],ct));
        MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])current!).Should().BeEquivalentTo(closed);
    }
    [Fact]
    public async Task Position_history_pages_include_all_rows_after_200_and_reject_cross_position_cursors()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var id=new StrategyPositionId(new TradeEntityId(Random.Shared.Next(30_000_000,40_000_000),1,1,1),Guid.NewGuid());
        var from=new DateTime(2026,1,1,12,0,0,DateTimeKind.Utc);var to=from.AddDays(220);

        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            for(var index=0;index<205;index++)
            {
                var at=from.AddDays(index);
                await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db,new StrategyPositionSnapshot {Id=id,StrategyKind=TradeStrategyKind.IronCondor,
                    PositionSequence=index+1,RouteGeneration=1,AsOfUtc=at,ValueDate=DateOnly.FromDateTime(at),
                    Phase=index==0?StrategyPositionPhase.Open:StrategyPositionPhase.EndOfDay,IsOpen=true,DailyPnl=-index},ct);
            }
            return true;
        });
        var seen=new List<StrategyPositionSnapshot>();byte[]? cursor=null;byte[]? firstCursor=null;
        do
        {
            var page=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeReadPositionHistoryAsync(db,id.PositionId,from,to,17,cursor,ct));
            page.Items.Length.Should().BeLessThanOrEqualTo(17);seen.AddRange(page.Items);
            cursor=page.PagingState;firstCursor??=cursor;
        }while(cursor is not null);
        seen.Should().HaveCount(205);seen.Select(x=>x.PositionSequence).Should().Equal(Enumerable.Range(1,205).Select(x=>(long)x));
        seen[0].Phase.Should().Be(StrategyPositionPhase.Open);
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeReadPositionHistoryAsync(db,Guid.NewGuid(),from,to,17,firstCursor,ct))).Should().ThrowAsync<ArgumentException>();
        var current=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeReadPositionAsync(db,id,ct));
        current!.PositionSequence.Should().Be(205);
    }

    [Fact]
    public async Task Native_context_loads_latest_order_position_history_and_committed_routes()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();var context=new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.TradeDb).Returns(context);
        var trade=new TradeEntityId(Random.Shared.Next(40_000_000,50_000_000),1,1,1);var now=DateTime.UtcNow;
        var order=new TradeOrderDefinition {Id=new TradeOrderId(trade.PortfolioId,trade.FundId,trade.OrderId),Revision=1,DefinitionHash="native-1"};
        await context.UpsertTradeOrderAsync(order,now);
        await context.UpsertTradeOrderAsync(order with {Revision=2,DefinitionHash="native-2"},now.AddSeconds(1));
        (await context.GetTradeOrderAsync(order.Id))!.Revision.Should().Be(2);
        var contract="native-context-"+Guid.NewGuid().ToString("N");
        var position=new StrategyPositionSnapshot {Id=new StrategyPositionId(trade,Guid.NewGuid()),StrategyKind=TradeStrategyKind.IronCondor,
            PositionSequence=1,RouteGeneration=1,AsOfUtc=now,ValueDate=DateOnly.FromDateTime(now),IsOpen=true,Phase=StrategyPositionPhase.Open,
            Legs=[new StrategyPositionLeg {TradeLegId=Guid.NewGuid(),ContractId=contract,SignedQuantity=1}]};
        await context.UpsertStrategyPositionAsync(position);await context.ReplaceOpenPositionRoutesAsync(position);
        (await context.GetStrategyPositionAsync(position.Id)).Should().BeEquivalentTo(position);
        (await context.GetStrategyPositionHistoryAsync(position.Id.PositionId,now.AddSeconds(-1),now.AddMinutes(1),10)).Items.Should().ContainSingle();
        (await context.GetOpenPositionRoutesAsync(contract)).Should().ContainSingle();
        (await context.GetOpenPositionRouteSnapshotAsync()).Should().Contain(x=>x.Route.StrategyPositionId==position.Id.PositionId);
        var page=await context.GetOpenPositionRoutePageAsync(2);page.Items.Should().HaveCountLessThanOrEqualTo(2);
        var closed=position with {PositionSequence=2,AsOfUtc=now.AddSeconds(2),IsOpen=false,Phase=StrategyPositionPhase.Close};
        await context.UpsertStrategyPositionAsync(closed);await context.ReplaceOpenPositionRoutesAsync(closed);
        (await context.GetOpenPositionRoutesAsync(contract)).Should().BeEmpty();
    }

    [Fact]
    public async Task Audit_only_reservation_is_not_a_committed_duplicate()
    {
        var id = new TradeOrderId(Random.Shared.Next(50_000_000, 60_000_000), 1, 1);
        var command = new CreateTradeOrderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, CreateTradeOrderCommand.Actor, CreateTradeOrderCommand.Verb, id.Format())
        };
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        { await db.ReserveCommandAsync(command, ct); return true; });
        var duplicate = await Assert.ThrowsAsync<CommandAuditDuplicateException>(() =>
            fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
            { await db.ReserveCommandAsync(command, ct); return true; }));
        duplicate.HasCommittedEvents.Should().BeFalse();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Command_audit_source_and_order_share_commit_and_duplicate_boundary(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var id=new TradeOrderId(Random.Shared.Next(50_000_000,60_000_000),1,1);var now=DateTime.UtcNow;
        var order=new TradeOrderDefinition {Id=id,Revision=1,DefinitionHash="audit-order"};
        var command=new CreateTradeOrderCommand {CommandId=Guid.NewGuid(),EntityId=id,Order=order,
            Subject=new ActorSubject(ActorType.Command,CreateTradeOrderCommand.Actor,CreateTradeOrderCommand.Verb,id.Format())};
        var source=new TradeOrderChangedEvent {Id=Guid.NewGuid(),EntityId=id,TradeOrderDefinition=order,CommandId=command.CommandId,ReceivedOn=now};
        async Task Save()=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.ReserveCommandAsync(command,ct);
            await db.AppendAsync(command.StreamId,command.CommandId,source,0,ct);
            await TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,order,now,ct);
            if(fail) throw new InvalidOperationException("Injected audit/source/material rollback");
            return true;
        });
        if(fail) await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>();else await Save();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(fail?0:1);
        var count=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM command_log WHERE commandid=$1;",[command.CommandId],ct));
        Convert.ToInt64(count).Should().Be(fail?0:1);
        if (fail)
        {
            fail = false;
            await Save();
            (await fixture.EventCountAsync(command.StreamId)).Should().Be(1);
        }
        if(!fail)
        {
            var duplicate = await Assert.ThrowsAsync<CommandAuditDuplicateException>(Save);
            duplicate.HasCommittedEvents.Should().BeTrue();
            await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await db.ReserveCommandAsync(command with {Order=order with {DefinitionHash="conflicting-command"}},ct);return true;})).Should().ThrowAsync<CommandAuditPayloadConflictException>();
            (await fixture.EventCountAsync(command.StreamId)).Should().Be(1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Monitoring_limits_commit_together_and_saved_values_are_available_without_live_feed(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var tradeId=Random.Shared.Next(70_000_000,80_000_000);var now=DateTime.UtcNow;
        var limit=new TradeLimitReadModel {TradeId=tradeId,TradeType=TradeType.ShortIronCondor,RiskMargin=1200.12m,MaxProfit=100.10m,MaxLoss=900.90m,
            MaxReturn=0.25m,MaxLossLimit=200.20m,MinProfitLimit=20.02m,MaxProfitLimit=80.08m,MinProfitTarget=30.03m,DailyProfitTarget=10.01m,
            CreatedOn=now,UpdatedOn=now,CreatedBy="fixture",UpdatedBy="fixture"};
        var spreads=new[] {new TradeTypeLimitReadModel {TradeId=tradeId,TradeType=TradeType.PutCreditSpread,MaxLossLimit=110.11m,MinProfitLimit=11.01m,MaxProfitLimit=40.04m},
            new TradeTypeLimitReadModel {TradeId=tradeId,TradeType=TradeType.CallCreditSpread,MaxLossLimit=90.09m,MinProfitLimit=9.01m,MaxProfitLimit=40.04m}};

        async Task Save()=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await TradeDbContextExtensions.TradeFlowProjectMonitoringLimitsAsync(db,limit,spreads,ct);
            if(fail) throw new InvalidOperationException("Injected limit projection rollback");
            return true;
        });
        if(fail) await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>();else await Save();
        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();var context=new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);factory.SetupGet(x=>x.TradeDb).Returns(context);
        var stored=await context.GetTradeLimitAsync(tradeId);var storedSpreads=await context.GetTradeTypeLimitsAsync(tradeId);
        if(fail) {stored.Should().BeNull();storedSpreads.Should().BeEmpty();}
        else
        {
            stored!.MaxLossLimit.Should().Be(limit.MaxLossLimit);stored.RiskMargin.Should().Be(limit.RiskMargin);
            storedSpreads.Should().BeEquivalentTo(spreads);
            await context.ProjectMonitoringLimitsAsync(limit,spreads);
            (await context.GetTradeTypeLimitsAsync(tradeId)).Should().HaveCount(2);
            (await context.GetTradeTypeLimitAsync(tradeId,TradeType.PutCreditSpread)).Should().BeEquivalentTo(spreads[0]);
        }
    }

    [Fact]
    public async Task Established_trade_and_approved_limits_rollback_commit_and_replay_together()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaSql.Create,[],ct));
        var settings = new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory = new Mock<IDbContextFactory>();
        var context = new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.TradeDb).Returns(context);
        var now = DateTime.UtcNow;
        var id = new TradeEntityId(Random.Shared.Next(50_000_000,60_000_000), 1, 1, Random.Shared.Next(70_000_000,80_000_000));
        var limit = new TradeLimitReadModel { TradeId = id.TradeId, TradeType = TradeType.ShortIronCondor,
            RiskMargin = 5000, MaxLoss = -700, MaxProfit = 1300, CreatedOn = now, UpdatedOn = now, CreatedBy = "PortfolioApproval", UpdatedBy = "PortfolioApproval" };
        var snapshot = new EstablishedTradeMonitoringLimits { PortfolioApprovalId = Guid.NewGuid(), OrderRevision = 1,
            DefinitionHash = "approved", TradeLimit = limit,
            SpreadLimits = [new(id.TradeId,TradeType.PutCreditSpread,16,2,2),new(id.TradeId,TradeType.CallCreditSpread,10,1.25m,1.25m)] };
        var trade = new EstablishedTradeDefinition { Id = id, StrategyKind = TradeStrategyKind.IronCondor,
            AssetFamily = TradeAssetFamily.FuturesOption, EvidenceRevision = 1, EstablishedAtUtc = now, MonitoringLimits = snapshot };
        var stream = "approved-limits-" + Guid.NewGuid().ToString("N");
        var commandId = Guid.NewGuid();
        var source = new TomasAI.IFM.Domain.Trade.Shared.Futures.Option.OptionTradeChangedEvent
            { Id = Guid.NewGuid(), EntityId = id, CommandId = commandId, ReceivedOn = now, State = trade };
        async Task Save(bool fail) => await fixture.Transactions.ExecuteOnceAsync(async (db,ct) =>
        {
            await db.AppendAsync(stream,commandId,source,0,ct);
            await db.TradeFlowProjectTradeAsync(trade,ct);
            if (fail) throw new InvalidOperationException("Injected failure after trade and limits");
            return true;
        });
        await FluentActions.Awaiting(() => Save(true)).Should().ThrowAsync<InvalidOperationException>();
        (await context.GetEstablishedTradeAsync(id)).Should().BeNull();
        (await context.GetTradeLimitAsync(id.TradeId)).Should().BeNull();
        (await context.GetTradeTypeLimitsAsync(id.TradeId)).Should().BeEmpty();
        (await fixture.EventCountAsync(stream)).Should().Be(0);
        await Save(false);
        (await context.GetEstablishedTradeAsync(id))!.MonitoringLimits.Should().BeEquivalentTo(snapshot);
        (await context.GetTradeLimitAsync(id.TradeId))!.MaxLoss.Should().Be(-700);
        (await context.GetTradeTypeLimitsAsync(id.TradeId)).Should().HaveCount(2);
        // Rebuilding the trade must not overwrite subsequent authoritative monitoring adjustments.
        await context.ProjectMonitoringLimitsAsync(limit with { MaxLoss = -600 },snapshot.SpreadLimits);
        await context.UpsertEstablishedTradeAsync(trade);
        (await context.GetTradeLimitAsync(id.TradeId))!.MaxLoss.Should().Be(-600);
    }

}
