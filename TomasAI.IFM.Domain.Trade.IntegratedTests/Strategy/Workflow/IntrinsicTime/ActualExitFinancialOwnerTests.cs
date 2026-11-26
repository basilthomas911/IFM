using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;
public sealed partial class TradeSelectionRuntimeTests
{
    // The controlled exit-required plan is a labelled input fixture. Every receiving owner,
    // composition/risk Function, reservation, order, broker execution and settlement is production code.
    static async Task<(TradeOrderDefinition Order,Guid Attempt,Func<IServiceProvider,Task> Replay)> ExecuteActualExitWorkflowAsync(
        IServiceProvider services,StrategyPositionSnapshot position)
    {
        var date=DateOnly.FromDateTime(DateTime.UtcNow);var planEvent=Guid.NewGuid();
        var workflow=new ExitPositionWorkflowId(position.Id,date,Guid.NewGuid());
        var plan=new StrategyTradePlanSnapshot{Position=position,ValueDate=date,PlanRevision=position.PositionSequence,
            State=TradePlanState.ExitRequired,Action=TradePlanAction.ExitAtMarket,RequiresExit=true,
            ReasonCode="VAL1.CONTROLLED_EXIT_PLAN",Explanation="Isolated financial owner qualification input",CalculatedAtUtc=DateTime.UtcNow};
        plan=plan with{ContentHash=FinancialCanonicalHash.Compute(plan)};
        ICommand<ExitPositionWorkflowId> start=position.StrategyKind switch
        {
            TradeStrategyKind.FuturesOutright=>new StartFuturesExitPositionWorkflowCommand
            {
                EntityId=workflow,CommandId=TradePlanContractIdentity.DeterministicId(workflow.Format()+"|start"),
                Subject=new(ActorType.Command,StartFuturesExitPositionWorkflowCommand.Actor,StartFuturesExitPositionWorkflowCommand.Verb,workflow.Format()),
                ExitPlan=new(){Id=planEvent,EntityId=new(position.Id,date),SourceEventId=planEvent,Plan=plan}
            },
            TradeStrategyKind.VerticalSpread=>new StartVerticalSpreadExitPositionWorkflowCommand
            {
                EntityId=workflow,CommandId=TradePlanContractIdentity.DeterministicId(workflow.Format()+"|start"),
                Subject=new(ActorType.Command,StartVerticalSpreadExitPositionWorkflowCommand.Actor,StartVerticalSpreadExitPositionWorkflowCommand.Verb,workflow.Format()),
                ExitPlan=new(){Id=planEvent,EntityId=new(position.Id,date),SourceEventId=planEvent,Plan=plan}
            },
            TradeStrategyKind.IronCondor=>new StartIronCondorExitPositionWorkflowCommand
            {
                EntityId=workflow,CommandId=TradePlanContractIdentity.DeterministicId(workflow.Format()+"|start"),
                Subject=new(ActorType.Command,StartIronCondorExitPositionWorkflowCommand.Actor,StartIronCondorExitPositionWorkflowCommand.Verb,workflow.Format()),
                ExitPlan=new(){Id=planEvent,EntityId=new(position.Id,date),SourceEventId=planEvent,Plan=plan}
            },
            _=>throw new InvalidOperationException("Unexpected qualification strategy.")
        };
        async Task Replay(IServiceProvider provider)
        {
            var actors=provider.GetRequiredService<IActorService>();
            ServiceResult<Guid> accepted=start switch
            {
                StartFuturesExitPositionWorkflowCommand command=>await actors.RequestAsync<StartFuturesExitPositionWorkflowCommand,ExitPositionWorkflowId>(command),
                StartVerticalSpreadExitPositionWorkflowCommand command=>await actors.RequestAsync<StartVerticalSpreadExitPositionWorkflowCommand,ExitPositionWorkflowId>(command),
                StartIronCondorExitPositionWorkflowCommand command=>await actors.RequestAsync<StartIronCondorExitPositionWorkflowCommand,ExitPositionWorkflowId>(command),
                _=>throw new InvalidOperationException()
            };
            accepted.Success.Should().BeTrue(accepted.ErrorMessage);accepted.Value.Should().Be(start.CommandId);
        }
        await Replay(services); // Deliberately discard the successful response, then retry the identical input.
        await Replay(services);
        var databases=services.GetRequiredService<IDbContextFactory>();var timer=System.Diagnostics.Stopwatch.StartNew();
        var quoted=false;ExitPositionWorkflowProjection? projection=null;TradeOrderDefinition? order=null;
        while(timer.Elapsed<TimeSpan.FromSeconds(45))
        {
            projection=await databases.TradePlanDb.GetCurrentExitWorkflowAsync(position.Id,date);
            if(projection is not null)
            {
                projection.WorkflowId.Should().Be(workflow);projection.SourcePlanEventId.Should().Be(planEvent);
                if(projection.RiskDecision?.TradeOrder is {} authorized)
                {
                    order=await databases.TradeDb.GetTradeOrderAsync(authorized.Id);
                    if(order?.BoundExecutionAttemptId is {} attempt)
                    {
                        var execution=await databases.TradeDb.GetOrderExecutionAsync(order.Id,attempt);
                        if(!quoted && execution?.Status==OrderExecutionStatus.Submitted)
                        {
                            await PublishOpeningFixtureQuotesAsync(services,new(){Order=order},CancellationToken.None,requireNewMatch:false);quoted=true;
                        }
                        if(execution?.Status==OrderExecutionStatus.Filled)
                        {
                            execution.Fills.Should().HaveCount(position.Legs.Length);
                            return (order,attempt,Replay);
                        }
                    }
                }
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"Actual exit workflow did not fill: strategy={position.StrategyKind},stage={projection?.State},reason={projection?.RiskDecision?.ReasonCode},order={order?.Status},quoted={quoted}");
    }
}
