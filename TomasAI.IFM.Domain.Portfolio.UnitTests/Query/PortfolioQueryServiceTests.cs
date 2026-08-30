using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Query;

public sealed class PortfolioQueryServiceTests
{
    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public void Page_tokens_are_opaque_typed_and_reject_cross_use_or_tampering()
    {
        var now = new DateTime(2026, 8, 30, 19, 0, 0, DateTimeKind.Utc);
        PortfolioPageToken.DecodeInteger(PortfolioPageToken.EncodeInteger(123)).Should().Be(123);
        PortfolioPageToken.DecodeTimestamp(PortfolioPageToken.EncodeTimestamp(now)).Should().Be(now);
        var crossUse = () => PortfolioPageToken.DecodeInteger(PortfolioPageToken.EncodeTimestamp(now));
        crossUse.Should().Throw<FormatException>();
        var tamper = () => PortfolioPageToken.DecodeInteger("not-base64");
        tamper.Should().Throw<FormatException>();
    }

    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public async Task Concurrency_queries_return_projection_aggregate_revisions_not_business_versions()
    {
        var service = new PortfolioQueryService(ProjectionCatalog.Valid(), new PortfolioFundStrategyResolver());

        var portfolio = await service.GetPortfolioRevisionAsync(101);
        var fund = await service.GetFundRevisionAsync(101, 201);

        portfolio.Value!.Revision.Should().Be(7).And.NotBe(2);
        fund.Value!.Revision.Should().Be(5).And.NotBe(3);
    }

    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-11")]
    [Trait("Category", "Portfolio")]
    public async Task Strategy_query_resolves_from_projection_context_only_and_maps_configuration_errors()
    {
        var db = ProjectionCatalog.Valid();
        var service = new PortfolioQueryService(db, new PortfolioFundStrategyResolver());
        var now = db.Now;

        var result = await service.GetStrategySnapshotAsync(101, 2026, "Daily", "ES", "Futures", now, Guid.NewGuid(), 1, Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Value!.Fund.FundId.Should().Be(201);
        result.Value.PayloadSha256.Should().HaveLength(64);

        db.Envelope = db.Envelope with { CapacityState = FundCapacityState.Blocked };
        var blocked = await service.GetStrategySnapshotAsync(101, 2026, "Daily", "ES", "Futures", now, Guid.NewGuid(), 1, Guid.NewGuid());
        blocked.Success.Should().BeFalse();
        blocked.ErrorMessage.Should().Contain("FundRiskEnvelopeBlocked");
    }

    sealed class ProjectionCatalog : IPortfolioDbReadContext
    {
        public DateTime Now { get; } = new(2026, 8, 30, 19, 0, 0, DateTimeKind.Utc);
        public PortfolioReadModel Portfolio { get; init; } = new();
        public FundMandateReadModel Fund { get; init; } = new();
        public FundAllocationReadModel Allocation { get; init; } = new();
        public FundRiskEnvelopeReadModel Envelope { get; set; } = new();
        public FundTradeTemplateAssignmentReadModel Assignment { get; init; } = new();

        public static ProjectionCatalog Valid()
        {
            var db = new ProjectionCatalog();
            var policy = Guid.NewGuid();
            return new ProjectionCatalog
            {
                Portfolio = new() { PortfolioId = 101, PortfolioCode = "CORE", Name = "Core", PortfolioVersion = 2, OperatingState = PortfolioOperatingState.Active, EffectiveFromUtc = db.Now.AddDays(-2), PolicyId = policy, PolicyVersion = 1, CreatedOnUtc = db.Now.AddDays(-2), CreatedBy = "admin" },
                Fund = new() { PortfolioId = 101, FundId = 201, FundCode = "DAILY", Name = "Daily", FundMandateVersion = 3, TradingYear = 2026, OperatingState = FundOperatingState.Active, EffectiveFromUtc = db.Now.AddDays(-1), DecisionHorizon = "Daily", Objective = "ES", UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"], PermittedTradeFamilies = ["DirectionalFuture"], CreatedOnUtc = db.Now.AddDays(-1), CreatedBy = "admin" },
                Allocation = new() { PortfolioId = 101, PortfolioVersion = 2, FundId = 201, FundMandateVersion = 3, AllocationVersion = 1, TargetWeight = .2m, MaximumWeight = .4m, AllocatedCapital = 100000, EffectiveFromUtc = db.Now.AddHours(-2), SourcePolicyVersion = 1, CreatedOnUtc = db.Now.AddHours(-2), CreatedBy = "admin" },
                Envelope = new() { PortfolioId = 101, PortfolioVersion = 2, FundId = 201, FundMandateVersion = 3, EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available, AllocatedCapital = 100000, AvailableCapital = 90000, MaximumRiskPerTrade = 1000, MaximumAggregateRisk = 5000, MaximumMargin = 20000, MaximumGrossNotional = 200000, MaximumContracts = 10, MaximumOpenPositions = 5, RemainingLossBudget = 10000, EffectiveFromUtc = db.Now.AddHours(-1), ExpiresAtUtc = db.Now.AddHours(1), SourcePolicyId = policy, SourcePolicyVersion = 1, CreatedOnUtc = db.Now.AddHours(-1), CreatedBy = "admin" },
                Assignment = new() { PortfolioId = 101, PortfolioVersion = 2, FundId = 201, FundMandateVersion = 3, AssignmentVersion = 1, TradeTemplateId = Guid.NewGuid(), TradeTemplateVersion = 1, Enabled = true, DecisionHorizon = "Daily", UnderlyingUniverse = ["ES"], AssetType = "Futures", TradeFamily = "DirectionalFuture", Priority = 1, EffectiveFromUtc = db.Now.AddHours(-1), TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1, OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1, CreatedOnUtc = db.Now.AddHours(-1), CreatedBy = "admin" },
            };
        }

        public Task<PortfolioReadModel?> GetPortfolioAsync(int portfolioId, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioReadModel?>(portfolioId == Portfolio.PortfolioId ? Portfolio : null);
        public Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioProjectionRevision?>(portfolioId == Portfolio.PortfolioId ? new(portfolioId, null, 7, 70) : null);
        public Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState state, int bucket, int afterPortfolioId, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortfolioReadModel>>([Portfolio]);
        public Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int portfolioId, int afterFundId, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FundMandateReadModel>>([Fund]);
        public Task<FundMandateReadModel?> GetFundAsync(int fundId, CancellationToken cancellationToken = default) => Task.FromResult<FundMandateReadModel?>(fundId == Fund.FundId ? Fund : null);
        public Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int fundId, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioProjectionRevision?>(fundId == Fund.FundId ? new(Fund.PortfolioId, fundId, 5, 71) : null);
        public Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int portfolioId, int tradingYear, string decisionHorizon, DateTime effectiveAtUtc, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FundMandateReadModel>>([Fund]);
        public Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FundTradeTemplateAssignmentReadModel>>([Assignment]);
        public Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) => Task.FromResult<FundAllocationReadModel?>(Allocation);
        public Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) => Task.FromResult<FundRiskEnvelopeReadModel?>(Envelope);
        public Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FundOrderProjectionReadModel>>([]);
        public Task<FundOrderProjectionReadModel?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default) => Task.FromResult<FundOrderProjectionReadModel?>(null);
        public Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int orderId, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FundOrderTradeProjectionReadModel>>([]);
        public Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default) => Task.FromResult<FundOrderTradeProjectionReadModel?>(null);
        public Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid workflowId, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>>([]);
    }
}
