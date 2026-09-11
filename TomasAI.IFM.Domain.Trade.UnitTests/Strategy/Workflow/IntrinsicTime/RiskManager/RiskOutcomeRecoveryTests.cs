using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;
public sealed class RiskOutcomeRecoveryTests
{
    [Fact]
    public void Projection_cursor_never_resets_or_moves_backwards()
    {
        RiskObservationRecoveryService.Advance(100,[]).Should().Be(100);
        RiskObservationRecoveryService.Advance(100,[90,95]).Should().Be(100);
        RiskObservationRecoveryService.Advance(100,[101,102]).Should().Be(102);
    }
    [Fact]
    public async Task Lost_fund_reply_recovers_from_committed_state_without_resending_or_renewing_expiry()
    {
        var input=await RiskFixture.Command();
        var source=new WorkflowStrategyStateUpdatedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowId=input.WorkflowId,State=new(){WorkflowId=input.WorkflowId,CurrentStage=StrategyWorkflowStage.RiskManagement,Status=WorkflowStrategyMachineStatus.TimedOut,TerminalAtUtc=input.ExpiresAtUtc,OrderComposition=new(){Result=input.CompositionResult}}};
        var evidence=source.TerminalRisk!;
        var order=new FundOrderProjectionReadModel {OrderId=evidence.OrderId,PortfolioId=evidence.PortfolioId,FundId=evidence.FundId,IdempotencyKey=Guid.NewGuid(),Status="RiskPending",AggregateVersion=3,WorkflowId=evidence.WorkflowId,CompositionResultHash=evidence.CompositionHash};
        var aggregate=new PortfolioFundAggregate();
        aggregate.Replay([new FundCompositionReserved(Guid.NewGuid(),Guid.NewGuid(),1,DateTime.UtcNow,"fixture",new(){Order=order,Trades=[new(){OrderId=order.OrderId}]})]);
        var funds=Substitute.For<IPortfolioEventStore>();funds.LoadFundAsync(Arg.Any<PortfolioFundId>(),Arg.Any<CancellationToken>()).Returns(aggregate);
        var actors=Substitute.For<IActorService>();var calls=0;
        actors.RequestAsync<PortfolioCommand<SynchronizeFundRiskOutcomePayload,PortfolioFundId>,PortfolioFundId>(Arg.Any<PortfolioCommand<SynchronizeFundRiskOutcomePayload,PortfolioFundId>>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            calls++;var command=call.Arg<PortfolioCommand<SynchronizeFundRiskOutcomePayload,PortfolioFundId>>();
            command.Payload.Evidence.Should().Be(evidence);command.RequestedOnUtc.Should().Be(input.ExpiresAtUtc);
            aggregate.Replay([new FundCompositionStateChanged(Guid.NewGuid(),command.CommandId,2,DateTime.UtcNow,"fixture",order with {AggregateVersion=4,Status="Expired",TerminalRisk=evidence})]);
            return ValueTask.FromException<ServiceResult<Guid>>(new TimeoutException("Reply lost after durable commit"));
        });
        var service=new RiskObservationRecoveryService(null!,null!,funds,actors,NullLogger<RiskObservationRecoveryService>.Instance);
        await FluentActions.Awaiting(()=>service.SynchronizeAsync(source,CancellationToken.None)).Should().ThrowAsync<TimeoutException>();
        var restarted=new RiskObservationRecoveryService(null!,null!,funds,actors,NullLogger<RiskObservationRecoveryService>.Instance);
        await restarted.SynchronizeAsync(source,CancellationToken.None);calls.Should().Be(1);
        aggregate.Composition(order.OrderId).Order.TerminalRisk!.DecidedAtUtc.Should().Be(input.ExpiresAtUtc);
    }
}
