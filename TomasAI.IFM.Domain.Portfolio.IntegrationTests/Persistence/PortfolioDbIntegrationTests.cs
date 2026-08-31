using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

public sealed class PortfolioDbFixture : IDisposable
{
    readonly PortfolioSchemaDb _schema;
    public PortfolioDbFixture()
    {
        var settings = new DbConnectionSettings().Add(PortfolioDbContext.PortfolioDbConnection,
            "Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db", "System.Data.ScyllaDb");
        var logger = Substitute.For<ILogger<DbProvider>>();
        _schema = new PortfolioSchemaDb(settings, logger);
        _schema.CreateAllAsync().GetAwaiter().GetResult();
        _schema.CreateAllAsync().GetAwaiter().GetResult();
        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        Db = new PortfolioDbContext(settings, factory, logger);
        repositories.Add(typeof(IObjectRepository<PortfolioDbContext>), Db);
    }
    public PortfolioDbContext Db { get; }
    public async Task ResetAsync()
    {
        await _schema.DropAllAsync();
        await _schema.CreateAllAsync();
    }
    public void Dispose() => _schema.DropAllAsync().GetAwaiter().GetResult();
}

[Collection(PortfolioPersistenceCollection.Name)]
public sealed class PortfolioDbIntegrationTests(PortfolioDbFixture fixture) : IClassFixture<PortfolioDbFixture>
{
    [Fact]
    [Trait("Gate", "PF-25")]
    [Trait("Category", "Portfolio")]
    public async Task Real_Scylla_policy_point_list_active_and_deletion_fence_paths_round_trip()
    {
        var value = Math.Abs(Guid.NewGuid().GetHashCode()) + 30_000;
        var now = new DateTime(2026, 8, 30, 19, 0, 0, DateTimeKind.Utc);
        var draft = new PortfolioFinancialPolicyReadModel
        {
            PortfolioId = value, PolicyId = value + 1, PolicyVersion = 1, AggregateRevision = 1,
            Name = "Integration limits", OperatingState = PortfolioFinancialPolicyState.Draft,
            CapitalBase = 1_000_000, MaximumDeployableCapital = 900_000, MaximumRiskPerTrade = 10_000,
            MaximumAggregateRisk = 100_000, MaximumMargin = 500_000, MaximumGrossNotional = 5_000_000,
            MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000,
            TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }],
            EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "integration"
        };
        var active = draft with { OperatingState = PortfolioFinancialPolicyState.Active, AggregateRevision = 2 };

        await fixture.Db.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(draft, 1, 100, now));
        await fixture.Db.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(active, 2, 101, now.AddSeconds(1)));

        (await fixture.Db.GetPolicyAsync(draft.PolicyId, 1)).Should().BeEquivalentTo(active);
        (await fixture.Db.GetPoliciesAsync(value, 10)).Should().ContainEquivalentOf(active);
        (await fixture.Db.GetActivePolicyAsync(value)).Should().BeEquivalentTo(active);

        var deleteId = value + 2;
        var deletable = draft with { PolicyId = deleteId };
        await fixture.Db.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(deletable, 1, 102, now));
        await fixture.Db.DeleteDraftPolicyAsync(new(value, deleteId, 103));
        (await fixture.Db.GetPolicyAsync(deleteId, 1)).Should().BeNull();

        // A delayed projector delivery from before the tombstone must not resurrect
        // either point or Portfolio-list projections.
        await fixture.Db.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(deletable, 1, 102, now));
        (await fixture.Db.GetPolicyAsync(deleteId, 1)).Should().BeNull();
        (await fixture.Db.GetPoliciesAsync(value, 10)).Should().NotContain(x => x.PolicyId == deleteId);
    }

    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Category", "Portfolio")]
    public async Task Real_Scylla_Draft_deletion_removes_parent_and_owned_Fund_configuration_projections()
    {
        var id = Math.Abs(Guid.NewGuid().GetHashCode()) + 20_000;
        var fundId = id + 1;
        var bucket = (id - 1) / 1000;
        var now = new DateTime(2026, 8, 30, 20, 0, 0, DateTimeKind.Utc);
        var portfolio = new PortfolioReadModel { PortfolioId=id,Name="Delete Draft",PortfolioVersion=1,OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="integration" };
        var fund = new FundMandateReadModel { PortfolioId=id,FundId=fundId,FundCode=$"F{fundId}",Name="Draft Fund",FundMandateVersion=1,TradingYear=2026,OperatingState=FundOperatingState.Draft,EffectiveFromUtc=now,DecisionHorizon="Daily",Objective="Draft",UnderlyingUniverse=["ES"],EligibleAssetTypes=["Futures"],PermittedTradeFamilies=["Futures"],CreatedOnUtc=now,CreatedBy="integration" };
        var assignment = new FundTradeTemplateAssignmentReadModel { PortfolioId=id,PortfolioVersion=1,FundId=fundId,FundMandateVersion=1,AssignmentVersion=1,TradeTemplateId=Guid.NewGuid(),TradeTemplateVersion=1,Enabled=true,DecisionHorizon="Daily",UnderlyingUniverse=["ES"],AssetType="Futures",TradeFamily="Futures",Priority=1,EffectiveFromUtc=now,TradeSelectionHintProfileId=Guid.NewGuid(),TradeSelectionHintProfileVersion=1,OrderCompositionProfileId=Guid.NewGuid(),OrderCompositionProfileVersion=1,CreatedOnUtc=now,CreatedBy="integration" };
        var allocation = new FundAllocationReadModel { PortfolioId=id,PortfolioVersion=1,FundId=fundId,FundMandateVersion=1,AllocationVersion=1,TargetWeight=.5m,MaximumWeight=1m,AllocatedCapital=100000,Currency="USD",EffectiveFromUtc=now,SourcePolicyId=9001,SourcePolicyVersion=1,CreatedOnUtc=now,CreatedBy="integration" };
        var envelope = new FundRiskEnvelopeReadModel { PortfolioId=id,PortfolioVersion=1,FundId=fundId,FundMandateVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=1,CapacityState=FundCapacityState.Available,Currency="USD",AllocatedCapital=100000,AvailableCapital=90000,MaximumRiskPerTrade=1000,MaximumAggregateRisk=5000,MaximumMargin=50000,MaximumGrossNotional=500000,MaximumContracts=10,MaximumOpenPositions=5,RemainingLossBudget=10000,EffectiveFromUtc=now,ExpiresAtUtc=now.AddDays(30),SourcePolicyId=9001,SourcePolicyVersion=1,CreatedOnUtc=now,CreatedBy="integration" };

        await fixture.Db.UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel>.Create(portfolio,1,11,now),bucket);
        await fixture.Db.UpsertFundAsync(PortfolioProjection<FundMandateReadModel>.Create(fund,1,12,now));
        await fixture.Db.UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel>.Create(assignment,1,13,now));
        await fixture.Db.UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel>.Create(allocation,1,14,now));
        await fixture.Db.UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel>.Create(envelope,1,15,now));

        await fixture.Db.DeleteDraftPortfolioAsync(new(id, bucket, [new(fundId, [1])], 100));

        (await fixture.Db.GetPortfolioAsync(id)).Should().BeNull();
        (await fixture.Db.GetPortfolioRevisionAsync(id)).Should().BeNull();
        (await fixture.Db.GetPortfoliosByStateAsync(PortfolioOperatingState.Draft,bucket,id-1,10)).Should().NotContain(x => x.PortfolioId == id);
        (await fixture.Db.GetFundsByPortfolioAsync(id,0,10)).Should().BeEmpty();
        (await fixture.Db.GetFundAsync(fundId)).Should().BeNull();
        (await fixture.Db.GetAssignmentsAsync(id,fundId,1,10)).Should().BeEmpty();
        (await fixture.Db.GetCurrentAllocationAsync(id,fundId)).Should().BeNull();
        (await fixture.Db.GetCurrentRiskEnvelopeAsync(id,fundId)).Should().BeNull();
    }

    [Fact]
    [Trait("Gate", "PF-08")]
    [Trait("Category", "Portfolio")]
    public async Task Real_Scylla_schema_and_all_typed_access_paths_round_trip_without_filtering()
    {
        var id = Math.Abs(Guid.NewGuid().GetHashCode()) + 10_000;
        var now = new DateTime(2026, 8, 29, 20, 0, 0, DateTimeKind.Utc);
        var portfolio = new PortfolioReadModel { PortfolioId=id,Name="Core",PortfolioVersion=1,OperatingState=PortfolioOperatingState.Active,ActivePolicyId=9001,ActivePolicyVersion=1,EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="integration" };
        var fund = new FundMandateReadModel { PortfolioId=id,FundId=id+1,FundCode=$"F{id}",Name="Daily",FundMandateVersion=1,TradingYear=2026,OperatingState=FundOperatingState.Active,EffectiveFromUtc=now,DecisionHorizon="Daily",Objective="Directional",UnderlyingUniverse=["ES"],EligibleAssetTypes=["Futures"],PermittedTradeFamilies=["Futures"],CreatedOnUtc=now,CreatedBy="integration" };
        var assignment = new FundTradeTemplateAssignmentReadModel { PortfolioId=id,PortfolioVersion=1,FundId=id+1,FundMandateVersion=1,AssignmentVersion=1,TradeTemplateId=Guid.NewGuid(),TradeTemplateVersion=1,Enabled=true,DecisionHorizon="Daily",UnderlyingUniverse=["ES"],AssetType="Futures",TradeFamily="Futures",Priority=1,EffectiveFromUtc=now,TradeSelectionHintProfileId=Guid.NewGuid(),TradeSelectionHintProfileVersion=1,OrderCompositionProfileId=Guid.NewGuid(),OrderCompositionProfileVersion=1,CreatedOnUtc=now,CreatedBy="integration" };
        var allocation = new FundAllocationReadModel { PortfolioId=id,PortfolioVersion=1,FundId=id+1,FundMandateVersion=1,AllocationVersion=1,TargetWeight=.5m,MaximumWeight=1m,AllocatedCapital=100000,Currency="USD",EffectiveFromUtc=now,SourcePolicyId=9001,SourcePolicyVersion=1,CreatedOnUtc=now,CreatedBy="integration" };
        var envelope = new FundRiskEnvelopeReadModel { PortfolioId=id,PortfolioVersion=1,FundId=id+1,FundMandateVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=1,CapacityState=FundCapacityState.Available,Currency="USD",AllocatedCapital=100000,AvailableCapital=90000,MaximumRiskPerTrade=1000,MaximumAggregateRisk=5000,MaximumMargin=50000,MaximumGrossNotional=500000,MaximumContracts=10,MaximumOpenPositions=5,RemainingLossBudget=10000,EffectiveFromUtc=now,ExpiresAtUtc=now.AddDays(30),SourcePolicyId=9001,SourcePolicyVersion=1,CreatedOnUtc=now,CreatedBy="integration" };
        var order = new FundOrderProjectionReadModel { PortfolioId=id,FundId=id+1,OrderId=id+2,WorkflowId=Guid.NewGuid(),Status="Reserved",CreatedOnUtc=now,CreatedBy="integration",AggregateVersion=1 };
        var trade = new FundOrderTradeProjectionReadModel { PortfolioId=id,FundId=id+1,OrderId=id+2,TradeId=id+3,TradeFamily="Futures",InstructionReference="ES",LegOrdinal=1,AggregateVersion=1 };
        var composition = new FundCompositionWorkflowProjectionReadModel { WorkflowId=order.WorkflowId,PortfolioId=id,FundId=id+1,OrderId=id+2,Status="Reserved",UpdatedOnUtc=now,AggregateVersion=1 };

        await fixture.Db.UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel>.Create(portfolio,1,1,now),0);
        await fixture.Db.UpsertFundAsync(PortfolioProjection<FundMandateReadModel>.Create(fund,1,2,now));
        await fixture.Db.UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel>.Create(assignment,1,3,now));
        await fixture.Db.UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel>.Create(allocation,1,4,now));
        await fixture.Db.UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel>.Create(envelope,1,5,now));
        await fixture.Db.UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel>.Create(order,1,6,now),new DateOnly(2026,8,1));
        await fixture.Db.UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel>.Create(trade,1,7,now));
        await fixture.Db.UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel>.Create(composition,1,8,now));
        var newerOrder = order with { Status="Composed", AggregateVersion=2 };
        await fixture.Db.UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel>.Create(newerOrder,2,100,now.AddSeconds(1)),new DateOnly(2026,8,1));
        await fixture.Db.UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel>.Create(order,1,99,now),new DateOnly(2026,8,1));

        (await fixture.Db.GetPortfolioAsync(id)).Should().BeEquivalentTo(portfolio);
        (await fixture.Db.GetPortfolioRevisionAsync(id)).Should().BeEquivalentTo(new PortfolioProjectionRevision(id, null, 1, 1));
        (await fixture.Db.GetPortfoliosByStateAsync(PortfolioOperatingState.Active,0,0,10)).Should().ContainEquivalentOf(portfolio);
        (await fixture.Db.GetFundsByPortfolioAsync(id,0,10)).Should().ContainEquivalentOf(fund);
        (await fixture.Db.GetFundAsync(id+1)).Should().BeEquivalentTo(fund);
        (await fixture.Db.GetFundRevisionAsync(id+1)).Should().BeEquivalentTo(new PortfolioProjectionRevision(id, id+1, 1, 2));
        (await fixture.Db.GetActiveFundsAsync(id,2026,"Daily",now.AddDays(1),10)).Should().ContainEquivalentOf(fund);
        (await fixture.Db.GetAssignmentsAsync(id,id+1,1,10)).Should().ContainEquivalentOf(assignment);
        (await fixture.Db.GetCurrentAllocationAsync(id,id+1)).Should().BeEquivalentTo(allocation);
        (await fixture.Db.GetCurrentRiskEnvelopeAsync(id,id+1)).Should().BeEquivalentTo(envelope);
        (await fixture.Db.GetOrdersAsync(id,id+1,new DateOnly(2026,8,1),now.AddSeconds(1),10)).Should().ContainEquivalentOf(newerOrder);
        (await fixture.Db.GetOrderAsync(id+2)).Should().BeEquivalentTo(newerOrder);
        (await fixture.Db.GetOrderTradesAsync(id+2,10)).Should().ContainEquivalentOf(trade);
        (await fixture.Db.GetTradeAsync(id+3)).Should().BeEquivalentTo(trade);
        (await fixture.Db.GetCompositionsAsync(order.WorkflowId,10)).Should().ContainEquivalentOf(composition);
    }
}
