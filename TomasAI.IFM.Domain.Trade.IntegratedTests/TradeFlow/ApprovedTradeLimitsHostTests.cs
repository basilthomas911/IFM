using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;
public sealed partial class TradeBrokerEmulatorHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hosted_approved_limits_commit_with_trade_or_recover_from_exact_original_evidence(bool recovery)
    {
        await using var host = sourceFactory.WithWebHostBuilder(ConfigureAuditHost);
        using var client = host.CreateClient();
        var lifecycle = host.Services.GetRequiredService<ISupervisorManagedActorLifecycle>();
        var producer = host.Services.GetRequiredService<IActorProducer>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await producer.StartAsync(new(ActorType.Realtime,"ApprovedLimitsVerification"));
        try
        {
            var container = host.Services.GetRequiredService<Container>();
            var factory = container.GetInstance<IDbContextFactory>();
            var transactions = container.GetInstance<IPostgresEventTransaction>();
            var orderId = Random.Shared.Next(10_000_000,20_000_000);
            var tradeId = Random.Shared.Next(20_000_000,30_000_000);
            var component = Guid.NewGuid(); var attempt = Guid.NewGuid(); var now = DateTime.UtcNow;
            var legs = new (byte side, decimal strike, int quantity, decimal price)[] { (1,6100,-1,10), (1,6150,1,5), (2,5900,-1,12), (2,5850,1,4) }
                .Select((x,i) => new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = $"ES20261218{(x.side==1?"C":"P")}{x.strike}",
                    ContractKey = $"ES20261218{(x.side==1?"C":"P")}{x.strike}", SignedQuantity = x.quantity,
                    AssetFamily = TradeAssetFamily.FuturesOption, PutCall = x.side, Strike = x.strike, CashMultiplier = 50, Expiry = new(2026,12,18) }).ToArray();
            var order = new TradeOrderDefinition { Id = new(1,1,orderId), Revision = 1, Status = TradeOrderStatus.Approved,
                PortfolioApprovalId = Guid.NewGuid(), DefinitionHash = "original-approved-limits", RequiredCapital = 2500,
                MaximumLoss = 1850, PositionType = TradeOrderPositionType.Opening, ValueDate = DateOnly.FromDateTime(now), ValidUntilUtc = now.AddMinutes(5),
                Components = [new() { ComponentId = component, ReservedTradeId = tradeId, StrategyKind = TradeStrategyKind.IronCondor, Legs = legs }] };
            var trade = new EstablishedTradeDefinition { Id = new(1,1,orderId,tradeId), AssetFamily = TradeAssetFamily.FuturesOption,
                StrategyKind = TradeStrategyKind.IronCondor, Status = EstablishedTradeStatus.Open, SourceComponentId = component,
                ExecutionAttemptId = attempt, EstablishedAtUtc = now, EvidenceRevision = 1, Legs = legs, OpeningCommission = 4,
                OriginalFills = legs.Select((leg,i) => new ExecutionFillEvidence { ExecutionFillId = Guid.NewGuid(), ExecutionAttemptId = attempt,
                    ComponentId = component, TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                    Price = new[] {10m,5m,12m,4m}[i], Commission = 1, FilledAtUtc = now }).ToArray() };
            await factory.TradeDb.UpsertTradeOrderAsync(order,now,deadline.Token);
            await transactions.ExecuteOnceAsync(async (db,ct) =>
            {
                await db.ExecuteAsync("INSERT INTO portfolio.order_composition_decision(operation_id,composition_id,workflow_id,portfolio_id,status,financial_revision,input_hash,receipt,committed_at_utc) VALUES($1,$2,$3,1,1,1,'fixture','{}'::jsonb,$4);",[order.PortfolioApprovalId,Guid.NewGuid(),Guid.NewGuid(),now],ct);
                await db.ExecuteAsync("INSERT INTO portfolio.accepted_trade_order(order_id,operation_id,portfolio_id,fund_id,status,definition_hash,definition,valid_until_utc) VALUES($1,$2,1,1,1,$3,$4::jsonb,$5);",[orderId,order.PortfolioApprovalId,order.DefinitionHash,System.Text.Json.JsonSerializer.Serialize(order.ToPortfolioInstruction()),order.ValidUntilUtc],ct);
                return true;
            },deadline.Token);
            if (!recovery) trade = trade with { MonitoringLimits = ApprovedTradeMonitoringLimits.Create(order,trade) };
            var create = new CreateOptionTradeCommand { EntityId = trade.Id, Trade = trade, CommandId = Guid.NewGuid(),
                Subject = new(ActorType.Command,FuturesOptionTradeActorNames.Command,CreateOptionTradeCommand.Verb,trade.Id.Format()) };
            var created = await producer.RequestAsync<CreateOptionTradeCommand,TradeEntityId,GuidResult>(create.Subject,create,trade.Id,deadline.Token);
            created.Success.Should().BeTrue(created.ErrorMessage);
            var position = StrategyPositionId.Create(trade.Id,TradeStrategyKind.IronCondor);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (await factory.TradeDb.GetStrategyPositionAsync(position,deadline.Token) is null && watch.Elapsed < TimeSpan.FromSeconds(30)) await Task.Delay(100,deadline.Token);
            (await factory.TradeDb.GetStrategyPositionAsync(position,deadline.Token)).Should().NotBeNull();
            if (recovery)
            {
                (await factory.TradeDb.GetTradeLimitAsync(tradeId,deadline.Token)).Should().BeNull();
                var recover = ApprovedTradeLimitRecovery.CreateCommand(order,trade);
                var bad = recover with { CommandId = Guid.NewGuid(), MonitoringInitialization = recover.MonitoringInitialization with { ApprovedOrder = order with { MaximumLoss = 1851 } } };
                var rejected = await producer.RequestAsync<InitializeIronCondorMonitoringCommand,StrategyPositionId,GuidResult>(bad.Subject,bad,position,deadline.Token);
                rejected.Success.Should().BeFalse();
                rejected.ErrorMessage.Should().Contain("CANONICAL_APPROVAL_MISMATCH");
                (await factory.TradeDb.GetTradeLimitAsync(tradeId,deadline.Token)).Should().BeNull();
                var count = await transactions.ExecuteOnceAsync((db,ct) => db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",[bad.CommandId],ct),deadline.Token);
                Convert.ToInt64(count).Should().Be(0);
                var repaired = await producer.RequestAsync<InitializeIronCondorMonitoringCommand,StrategyPositionId,GuidResult>(recover.Subject,recover,position,deadline.Token);
                repaired.Success.Should().BeTrue(repaired.ErrorMessage);
            }
            var saved = await factory.TradeDb.GetTradeLimitAsync(tradeId,deadline.Token);
            saved!.MaxLoss.Should().Be(-1850);
            saved.RiskMargin.Should().Be(2500);
            saved.MaxProfit.Should().Be(650);
            // Exercise the same public NATS queries used by Load Trade, not just storage reads.
            var queryApi = new TomasAI.IFM.Application.Api.Nats.Client.OptionTradeQueryApi(producer);
            var queried = await queryApi.GetTradeLimitAsync(tradeId);
            queried.Success.Should().BeTrue(queried.ErrorMessage);
            queried.Value!.TradeId.Should().Be(tradeId);
            queried.Value.MaxLoss.Should().Be(-1850);
            var spread = await queryApi.GetTradeTypeLimitAsync(tradeId,TradeType.PutCreditSpread);
            spread.Success.Should().BeTrue(spread.ErrorMessage);
            spread.Value!.TradeId.Should().Be(tradeId);
            (await factory.TradeDb.GetTradeTypeLimitsAsync(tradeId)).Should().HaveCount(2);
            var reloaded = await factory.TradeDb.GetEstablishedTradeAsync(trade.Id,deadline.Token);
            reloaded!.OriginalFills.Should().BeEquivalentTo(trade.OriginalFills);
        }
        finally
        {
            await lifecycle.ShutdownActorsAsync(CancellationToken.None);
            await host.Services.GetRequiredService<ISupervisorBootstrap>().StopSupervisorAsync(CancellationToken.None);
            await producer.StopAsync();
        }
    }
}
