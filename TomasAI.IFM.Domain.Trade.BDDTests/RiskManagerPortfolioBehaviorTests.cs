using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.BDDTests;

public sealed class RiskManagerPortfolioBehaviorTests
{
    [Fact]
    public async Task One_market_opportunity_is_accepted_independently_for_each_eligible_fund()
    {
        var view = await NeutralView();
        var request = PortfolioOrderCompositionMapper.CreateRequest(view, 1, view.UpdatedAtUtc);
        var deployment = request.Body.DeploymentKey;
        var book = new FinancialBookConfiguration
        {
            PortfolioId = 1,
            MigrationQualified = true,
            Funds =
            [
                Fund(10, true, deployment),
                Fund(11, false, deployment),
                Fund(12, true, deployment)
            ]
        };

        var receipt = await PortfolioOrderCompositionModel.EvaluateAsync(
            request, book, 1, book.Funds.Select(fund => new PortfolioFundFinancialSnapshot(
                fund.FundId,1_000_000,[])).ToArray(), new IdentityAllocator());

        receipt.Status.Should().Be(PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        receipt.TradeOrders.Select(order => order.Id.FundId).Should().Equal(10, 12);
        receipt.TradeOrders.Should().OnlyContain(order => order.Id.OrderId > 0 &&
            order.Components.All(component => component.ReservedTradeId > 0) &&
            order.Components.SelectMany(component => component.Legs)
                .All(leg => !string.IsNullOrWhiteSpace(leg.ContractId)));
        receipt.FundDecisions.Single(decision => decision.FundId == 11).Accepted.Should().BeFalse();
    }

    static FinancialFundAuthority Fund(int id, bool canSpend,
        TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey deployment) => new()
    {
        FundId = id,
        CanSpend = canSpend,
        Limits = Limits(CapacityScopeKind.Portfolio,FinancialScopeKeys.Portfolio(1))
            .Concat(Limits(CapacityScopeKind.Fund,FinancialScopeKeys.Fund(id))).ToArray(),
        Deployments = [new FinancialDeploymentAuthority(
            new FinancialAuthorityReference
            {
                DeploymentKey = deployment,
                ValidUntilUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }, Limits(CapacityScopeKind.Deployment,FinancialScopeKeys.Deployment(deployment)), 0)]
    };

    static CapacityLimit[] Limits(CapacityScopeKind scope,string key) =>
    [
        new(scope,key,CapacityMeasure.SettlementCash,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.LossCharge,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.Margin,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.GrossNotional,CapacityUnit.Usd,10_000_000),
        new(scope,key,CapacityMeasure.PositionSlots,CapacityUnit.Positions,100),
        new(scope,key,CapacityMeasure.GrossContracts,CapacityUnit.Contracts,100)
    ];

    static async Task<IntrinsicTimeStrategyWorkflowView> NeutralView()
    {
        var command = await CompositionFixture.Command("LongFuture");
        var result = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(
            new Black76ComposerPricer()).Calculate(command);
        result = result with
        {
            Candidate = result.Candidate! with
            {
                PortfolioId = 0, FundId = 0, OrderId = 0, PrimaryTradeId = 0
            },
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

    sealed class IdentityAllocator : IPortfolioBusinessIdAllocator
    {
        int order;
        int trade;
        public ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(++order);
        public ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(++trade);
        public ValueTask<Domain.Portfolio.Shared.Identities.PortfolioId> AllocatePortfolioIdAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
