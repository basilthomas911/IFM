using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

public class FundCompositionHandoffTests
{
    [Theory]
    [InlineData("TemplateSelected", 1, 1)]
    [InlineData("Composing", 0, 1)]
    [InlineData("RiskPending", 0, 0)]
    public async Task Replay_resumes_only_missing_fund_transitions(string status, int composingCalls, int composedCalls)
    {
        var (view, context, commands) = await Scenario(status);
        await ExecuteRiskManagement.EnsureFundCompositionAsync(view, context);
        await commands.Received(composingCalls).MarkComposingAsync(Arg.Any<PortfolioFundOrderId>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await commands.Received(composedCalls).RecordComposedAsync(Arg.Any<PortfolioFundOrderId>(), Arg.Any<long>(), Arg.Any<OrderCompositionResultReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_different_workflow_order_cannot_be_mutated()
    {
        var (view, context, commands) = await Scenario("TemplateSelected", true);
        await FluentActions.Awaiting(() => ExecuteRiskManagement.EnsureFundCompositionAsync(view, context)).Should().ThrowAsync<ArgumentException>();
        commands.ReceivedCalls().Should().BeEmpty();
    }

    static async Task<(IntrinsicTimeStrategyWorkflowView, IIntrinsicTimeStrategyWorkflowRealtimeContext, IPortfolioFundCommandApi)> Scenario(string status, bool wrongWorkflow = false)
    {
        var input = await CompositionFixture.Command();
        var result = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()).Calculate(input);
        var envelope = StrategyStageResultEnvelope.CreateComposition(result);
        var view = input.WorkflowView with { CompositionExecution = input, OrderComposition = new() { Result = envelope } };
        var order = input.Reservation.Order with { Status = status, WorkflowId = wrongWorkflow ? Guid.NewGuid() : input.WorkflowId.Value,
            CompositionResultId = result.ResultId, CompositionResultHash = envelope.PayloadSha256 };
        var queries = Substitute.For<IPortfolioQueryApi>();
        var commands = Substitute.For<IPortfolioFundCommandApi>();
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
        context.PortfolioQueries.Returns(queries); context.PortfolioCommands.Returns(commands);
        queries.GetOrderAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundOrderProjectionReadModel>(order));
        commands.MarkComposingAsync(Arg.Any<PortfolioFundOrderId>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FundOrderProjectionReadModel>(order with { Status = "Composing" }));
        commands.RecordComposedAsync(Arg.Any<PortfolioFundOrderId>(), Arg.Any<long>(), Arg.Any<OrderCompositionResultReference>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FundOrderProjectionReadModel>(order with { Status = "RiskPending" }));
        return (view, context, commands);
    }
}
