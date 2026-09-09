using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;
public sealed partial class TradeSelectionRuntimeTests
{
    [Fact,Trait("Category","RiskDeliveryRuntime")]
    public async Task Risk_history_replays_out_of_order_detects_conflicts_and_queries_bounded_scoped_pages_over_NATS()
    {
        var broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")??throw new InvalidOperationException("Isolated broker required.");
        var funds=Substitute.For<IPortfolioEventStore>();
        await using var host=Host(services=>{services.AddSingleton<RiskHistoryJournal>();services.AddSingleton(funds);},brokerUrl:broker);_=host.CreateClient();
        var supervisor=host.Services.GetRequiredService<IActorSupervisor>();var producer=host.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime,$"RiskHistory{Guid.NewGuid():N}"));
        try
        {
            await host.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            var db=host.Services.GetRequiredService<IDbContextFactory>().TradeDb;
            var input=await RiskFixture.Command();var result=new RiskEvaluator().Calculate(input);
            var first=new WorkflowStrategyStateUpdatedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=input.WorkflowEntityId,WorkflowId=input.WorkflowId,WorkflowRevision=8,UpdatedAtUtc=input.EvaluatedAtUtc,ReceivedOn=DateTime.UtcNow,
                State=new(){EntityId=input.WorkflowEntityId,WorkflowId=input.WorkflowId,WorkflowRevision=8,CurrentStage=StrategyWorkflowStage.RiskManagement,RiskExecution=input,
                    OrderComposition=new(){Result=input.CompositionResult},RiskManagement=new(){Result=StrategyStageResultEnvelope.CreateRisk(result)}}};
            var last=first with {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowRevision=9,State=first.State with {WorkflowRevision=9,StopReasonCode="terminal"}};
            await db.UpsertRiskHistoryAsync(last);await db.UpsertRiskHistoryAsync(first);await db.UpsertRiskHistoryAsync(last);
            (await db.GetRiskInvocationAsync(input.WorkflowId.Value,input.CommandId))!.WorkflowRevision.Should().Be(9);
            await FluentActions.Awaiting(()=>db.UpsertRiskHistoryAsync(last with {State=last.State with {StopReasonCode="conflicting"}})).Should().ThrowAsync<InvalidDataException>();
            var row=RiskHistoryIdentity.Row(last)!;var api=new RiskQueryApi(producer);
            var aggregate=new PortfolioFundAggregate();
            aggregate.Replay([new FundCompositionReserved(Guid.NewGuid(),Guid.NewGuid(),1,DateTime.UtcNow,"fixture",new(){Order=new(){PortfolioId=row.PortfolioId,FundId=row.FundId,OrderId=checked((int)row.OrderId),IdempotencyKey=Guid.NewGuid(),Status="RiskPending"},Trades=[new(){OrderId=checked((int)row.OrderId)}]})]);
            funds.LoadFundAsync(new PortfolioFundId(row.PortfolioId,row.FundId),Arg.Any<CancellationToken>()).Returns(aggregate);
            var exact=await api.GetInvocationAsync(input.WorkflowId,input.CommandId);
            exact.Success.Should().BeTrue(exact.ErrorMessage);exact.Value!.Calculation!.ResultId.Should().Be(result.ResultId);
            exact.Value.CurrentAuthority.Should().Be("Not authorized");
            (await api.GetResultAsync(input.WorkflowId,input.CommandId,result.ResultId)).Success.Should().BeTrue();
            (await api.GetResultAsync(input.WorkflowId,input.CommandId,Guid.NewGuid())).Success.Should().BeFalse();
            funds.LoadFundAsync(new PortfolioFundId(row.PortfolioId,row.FundId),Arg.Any<CancellationToken>()).Returns(Task.FromException<PortfolioFundAggregate>(new IOException("Authoritative Fund unavailable")));
            var unavailable=await api.GetInvocationAsync(input.WorkflowId,input.CommandId);
            unavailable.Success.Should().BeTrue(unavailable.ErrorMessage);unavailable.Value!.CurrentAuthority.Should().Be("Unavailable");unavailable.Value.Calculation!.ResultId.Should().Be(result.ResultId);
            funds.LoadFundAsync(new PortfolioFundId(row.PortfolioId,row.FundId),Arg.Any<CancellationToken>()).Returns(aggregate);
            var page=await api.GetHistoryAsync(row.PortfolioId,row.FundId,row.ValueDate,1);
            page.Success.Should().BeTrue(page.ErrorMessage);page.Value!.Items.Should().HaveCount(1);
            var found=false;var pages=0;
            while(true)
            {
                found|=page.Value!.Items.Any(x=>x.InvocationId==input.CommandId && x.Revision==9);
                if(page.Value.PagingState is null)break;
                (++pages).Should().BeLessThan(500);
                page=await api.GetHistoryAsync(row.PortfolioId,row.FundId,row.ValueDate,1,page.Value.PagingState);
                page.Success.Should().BeTrue(page.ErrorMessage);
            }
            found.Should().BeTrue();
            (await api.GetHistoryAsync(row.PortfolioId,row.FundId+100,row.ValueDate,1)).Success.Should().BeFalse();
            (await api.GetHistoryAsync(row.PortfolioId,row.FundId,row.ValueDate,101)).Success.Should().BeFalse();
            var events=host.Services.GetRequiredService<IEventSourceActorDbContext>();
            await events.SaveEventsAsync($"RiskHistoryTest.{last.CommandId}",last.CommandId,new DomainEventCollection([last]),0,CancellationToken.None);
            var journal=new RiskHistoryJournal(host.Services.GetRequiredService<TomasAI.IFM.Application.Storage.EventSourceDb.IPostgresEventTransaction>());
            var committed=await journal.ByCommandAsync(last.CommandId);
            committed.Should().BeOfType<WorkflowStrategyStateUpdatedEvent>();
            var originalCursor=await journal.LoadCursorAsync();
            var fundCursor=await journal.LoadCursorAsync(default,"risk-fund-outcomes-v1");
            try
            {
                await journal.SaveCursorAsync(committed!.EventId);
                (await journal.LoadCursorAsync(default,"risk-fund-outcomes-v1")).Should().Be(fundCursor);
                (await new RiskHistoryJournal(host.Services.GetRequiredService<TomasAI.IFM.Application.Storage.EventSourceDb.IPostgresEventTransaction>()).LoadCursorAsync()).Should().Be(committed.EventId);
                (await journal.PageAsync(committed.EventId-1)).Should().Contain(x=>x.CommandId==last.CommandId);
            }
            finally{await journal.SaveCursorAsync(originalCursor);}
            await db.UpsertRiskHistoryAsync((WorkflowStrategyStateUpdatedEvent)committed!);
            (await db.GetRiskInvocationAsync(input.WorkflowId.Value,input.CommandId))!.State.StopReasonCode.Should().Be("terminal");
        }
        finally{await supervisor.ShutdownAsync();await producer.StopAsync();}
    }
}
