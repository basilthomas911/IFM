using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

public sealed class PortfolioOrderCompositionMapperTests
{
    [Fact]
    public async Task Neutral_composition_maps_canonical_contracts_without_allocating_business_ids()
    {
        var view = await NeutralView("BullCallDebit");
        var at = view.UpdatedAtUtc;

        var request = PortfolioOrderCompositionMapper.CreateRequest(view, 7, at);
        var candidate = view.OrderComposition.Result!.ReadCompositionResult().Candidate!;

        request.PortfolioId.Should().Be(7);
        request.ExpectedFinancialRevision.Should().Be(0);
        request.Body.Components.SelectMany(value => value.Legs)
            .Select(value => value.ContractId).Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value));
        request.Body.Components.Should().OnlyContain(value => value.ReservedTradeId == 0);
        request.Body.ProductSymbol.Should().Be(candidate.Product.Symbol);
        request.Body.ProductExchange.Should().Be(candidate.Product.Exchange);
        request.Body.ProductCurrency.Should().Be(candidate.Product.Currency);
        request.Body.Delta.Should().Be(candidate.Greeks.Delta);
        request.Body.Gamma.Should().Be(candidate.Greeks.Gamma);
        request.Body.Vega.Should().Be(candidate.Greeks.Vega);
        request.InputSha256.Should().HaveLength(64);
    }

    [Fact]
    public async Task Mapping_is_deterministic_for_the_same_workflow_revision_and_composition()
    {
        var view = await NeutralView("LongFuture");

        var first = PortfolioOrderCompositionMapper.CreateRequest(view, 1, view.UpdatedAtUtc);
        var second = PortfolioOrderCompositionMapper.CreateRequest(view, 1, view.UpdatedAtUtc);

        second.OperationId.Should().Be(first.OperationId);
        second.CommandId.Should().Be(first.CommandId);
        second.Body.Components.Single().ComponentId.Should().Be(first.Body.Components.Single().ComponentId);
        second.Body.Components.Single().Legs.Single().TradeLegId
            .Should().Be(first.Body.Components.Single().Legs.Single().TradeLegId);
        second.InputSha256.Should().Be(first.InputSha256);
    }

    [Fact]
    public async Task Portfolio_receipt_status_and_identity_are_validated_before_workflow_completion()
    {
        var view = await NeutralView("LongFuture");
        var composition = view.OrderComposition.Result!.ReadCompositionResult();
        var invalid = new PortfolioOrderCompositionReceipt
        {
            CompositionId = composition.ResultId,
            WorkflowId = view.WorkflowId.Value,
            PortfolioId = 1,
            Status = PortfolioOrderCompositionStatus.ExecuteTradeOrders,
            FinancialRevision = 1
        };

        var action = () => PortfolioOrderCompositionMapper.ValidateReceipt(view, invalid, 1);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("RM.PORTFOLIO.RECEIPT_STATUS");
    }

    [Fact]
    public async Task Portfolio_completion_envelope_must_match_the_exact_request()
    {
        var view = await NeutralView("LongFuture");
        var request = PortfolioOrderCompositionMapper.CreateRequest(view, 1, view.UpdatedAtUtc);
        var completed = new PortfolioOrderCompositionCompletedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = request.CommandId,
            OperationId = request.OperationId,
            PortfolioId = request.PortfolioId,
            EntityId = request.EntityId,
            CorrelationId = request.CorrelationId,
            CausationId = request.CausationId,
            AggregateId = request.EntityId.Format(),
            InputHash = new string('0', 64),
            CommittedAtUtc = view.UpdatedAtUtc,
            Receipt = new PortfolioOrderCompositionReceipt
            {
                CompositionId = request.Body.CompositionId,
                WorkflowId = request.Body.WorkflowId,
                PortfolioId = request.PortfolioId,
                Status = PortfolioOrderCompositionStatus.NoTradeOrders,
                FinancialRevision = 1
            }
        };

        var action = () => PortfolioOrderCompositionMapper.ValidateCompletion(view, request, completed);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("RM.PORTFOLIO.COMPLETION_IDENTITY");
    }

    static async Task<IntrinsicTimeStrategyWorkflowView> NeutralView(string variant)
    {
        var command = await CompositionFixture.Command(variant);
        var result = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(
            new Black76ComposerPricer()).Calculate(command);
        var candidate = result.Candidate! with
        {
            PortfolioId = 0,
            FundId = 0,
            OrderId = 0,
            PrimaryTradeId = 0
        };
        result = result with
        {
            Candidate = candidate,
            DecisionContext = result.DecisionContext with { PortfolioId = 0, FundId = 0 }
        };
        return command.WorkflowView with
        {
            WorkflowRevision = 6,
            UpdatedAtUtc = command.EvaluatedAtUtc,
            SelectionBinding = command.SelectionBinding with { SchemaVersion = 2 },
            CompositionExecution = command,
            OrderComposition = command.WorkflowView.OrderComposition with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                SourceEventId = result.ResultId,
                Result = StrategyStageResultEnvelope.CreateComposition(result)
            }
        };
    }
}
