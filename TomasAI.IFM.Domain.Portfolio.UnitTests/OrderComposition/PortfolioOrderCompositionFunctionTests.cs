using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.OrderComposition;

public sealed class PortfolioOrderCompositionFunctionTests
{
    [Fact]
    [Trait("Gate", "PPG-07")]
    public void Completed_receipt_is_mapped_to_the_exact_function_identity()
    {
        var request = Request();
        var receipt = new PortfolioOrderCompositionReceipt
        {
            CompositionId=request.Body.CompositionId, WorkflowId=request.Body.WorkflowId,
            Status=PortfolioOrderCompositionStatus.NoTradeOrders, FinancialRevision=8
        };

        var result = new FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>(
            typeof(PortfolioOrderCompositionCompletedEvent), request, receipt).Complete(new Clock(request.RequestedAtUtc));

        result.IsCompleted.Should().BeTrue();
        result.Completed!.OperationId.Should().Be(request.OperationId);
        result.Completed.PortfolioId.Should().Be(request.PortfolioId);
        result.Completed.InputHash.Should().Be(request.InputSha256);
        result.Completed.Receipt.Should().BeSameAs(receipt);
    }

    [Fact]
    [Trait("Gate", "PPG-07")]
    public void Committed_or_replayed_event_is_returned_without_reconstruction()
    {
        var request = Request();
        var committed = new PortfolioOrderCompositionCompletedEvent
        {
            Id=Guid.NewGuid(), EntityId=request.EntityId, CommandId=request.CommandId,
            OperationId=request.OperationId, PortfolioId=request.PortfolioId, InputHash=request.InputSha256
        };

        var result = new FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>(
            typeof(PortfolioOrderCompositionCompletedEvent), request, committed,
            Phase:FunctionEventPhase.Replayed).Complete(new Clock(request.RequestedAtUtc));

        result.Completed.Should().BeSameAs(committed);
    }

    [Fact]
    [Trait("Gate", "PPG-07")]
    public void Failure_retains_stage_and_complete_exception_detail()
    {
        var request = Request();
        var exception = new InvalidOperationException("authority revision changed", new ArgumentException("revision"));

        var result = new FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>(
            typeof(PortfolioOrderCompositionFailedEvent), request, Exception:exception,
            Stage:FunctionFailureStage.Persistence).Fail(new Clock(request.RequestedAtUtc));

        result.IsFailed.Should().BeTrue();
        result.Failed!.ErrorMessage.Should().Be("authority revision changed");
        result.Failed.ErrorData.Should().Contain("InvalidOperationException").And.Contain("ArgumentException");
    }

    [Fact]
    [Trait("Gate", "PPG-07")]
    public void Every_executable_stage_selects_atomic_business_and_event_completion()
    {
        var request = Request();
        var context = new Context(new Clock(request.RequestedAtUtc));
        foreach (var stage in new[] { FunctionFailureStage.Loading, FunctionFailureStage.Execution,
                     FunctionFailureStage.Projection, FunctionFailureStage.Persistence })
            request.ResolveExecutionPolicy(stage, context).CompletionMode
                .Should().Be(FunctionCompletionMode.AtomicBusinessAndEvent);
    }

    [Fact]
    [Trait("Gate", "PPG-07")]
    public void State_accepts_one_matching_completion_and_rejects_a_second_completion()
    {
        var request = Request();
        var completed = new PortfolioOrderCompositionCompletedEvent
        {
            EntityId=request.EntityId, CommandId=request.CommandId, OperationId=request.OperationId,
            PortfolioId=request.PortfolioId, InputHash=request.InputSha256
        };
        var state = new PortfolioOrderCompositionFunctionState();

        state.TryComplete(completed, request).Should().BeTrue();
        state.Matches(request).Should().BeTrue();
        state.TryComplete(completed, request).Should().BeFalse();
        state.Matches(request with { InputSha256=new('f',64) }).Should().BeFalse();
    }

    static EvaluatePortfolioOrderCompositionCommand Request()
    {
        var now = new DateTime(2026,9,12,14,0,0,DateTimeKind.Utc);
        var operation = Guid.NewGuid();
        return new()
        {
            CommandId=Guid.NewGuid(), OperationId=operation, PortfolioId=12,
            EntityId=new FinancialExecutionId(12,operation), RequestedAtUtc=now,
            ExpiresAtUtc=now.AddMinutes(2), InputSha256=new('a',64),
            Body=new() { CompositionId=Guid.NewGuid(), WorkflowId=Guid.NewGuid() }
        };
    }

    sealed class Clock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()=>new(utc);
    }

    sealed class Context(TimeProvider clock) : IPortfolioOrderCompositionFunctionContext
    {
        public ActorMailboxId ActorId=>new(ActorType.Function,PortfolioOrderCompositionFunctionActor.ActorName);
        public IContainerInstance Container=>null!;
        public IEventSourceFunctionStateRepository<PortfolioOrderCompositionFunctionState,EvaluatePortfolioOrderCompositionCommand> StateRepository=>null!;
        public TimeProvider TimeProvider=>clock;
        public ILogger<PortfolioOrderCompositionFunctionActor> Logger=>NullLogger<PortfolioOrderCompositionFunctionActor>.Instance;
    }
}
