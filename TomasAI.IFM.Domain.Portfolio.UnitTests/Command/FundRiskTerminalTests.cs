using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;
public sealed class FundRiskTerminalTests
{
    [Theory]
    [InlineData("RiskRejected")] [InlineData("Expired")] [InlineData("Cancelled")]
    public void Committed_terminal_outcome_survives_delay_and_replays_idempotently(string status)
    {
        var (aggregate,evidence)=Fixture(status);
        var first=aggregate.SynchronizeRisk(3,evidence);
        first.Status.Should().Be(status);first.TerminalRisk.Should().Be(evidence);first.AggregateVersion.Should().Be(4);
        aggregate.SynchronizeRisk(4,evidence).Should().Be(first);
        var conflict=()=>aggregate.SynchronizeRisk(4,evidence with {Reason="changed"});conflict.Should().Throw<InvalidOperationException>();
    }
    [Theory]
    [InlineData("fund")] [InlineData("hash")] [InlineData("workflow")] [InlineData("target")] [InlineData("source")]
    public void Mismatched_terminal_evidence_cannot_change_the_order(string field)
    {
        var (aggregate,evidence)=Fixture("RiskRejected");
        evidence=field switch {"fund"=>evidence with {FundId=99},"hash"=>evidence with {CompositionHash="wrong"},"workflow"=>evidence with {WorkflowId=Guid.NewGuid()},"target"=>evidence with {TargetStatus="RiskApproved"},_=>evidence with {SourceEventId=Guid.Empty}};
        var action=()=>aggregate.SynchronizeRisk(3,evidence);action.Should().Throw<InvalidOperationException>();
        aggregate.ReservationForOrder(7).Order.Status.Should().Be("RiskPending");
    }
    static (PortfolioFundCompositionAggregate,RiskTerminalEvidence) Fixture(string status)
    {
        var workflow=Guid.NewGuid();var aggregate=new PortfolioFundCompositionAggregate();
        aggregate.Restore([new(){Order=new(){PortfolioId=1,FundId=2,OrderId=7,IdempotencyKey=Guid.NewGuid(),WorkflowId=workflow,CompositionResultHash=new('A',64),Status="RiskPending",AggregateVersion=3},Trades=[new(){OrderId=7}]}]);
        return (aggregate,new(Guid.NewGuid(),Guid.NewGuid(),workflow,1,2,7,new('A',64),status,Guid.NewGuid(),new('B',64),"No feasible quantity",DateTime.UtcNow.AddDays(-1)));
    }
}
