using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Projection;

public sealed class PortfolioProjectionHandlerTests
{
    [Fact]
    [Trait("Gate", "PF-25")]
    [Trait("Category", "Portfolio")]
    public async Task Policy_projection_maps_all_changed_versions_with_one_monotonic_event_fence()
    {
        var now = new DateTime(2026, 8, 30, 18, 0, 0, DateTimeKind.Utc);
        var id = new PortfolioFinancialPolicyId(101, 9001);
        var aggregate = new PortfolioFinancialPolicyAggregate();
        aggregate.Create(Guid.NewGuid(), Guid.NewGuid(), Policy(now), now, "unit");
        aggregate.Activate(Guid.NewGuid(), 1, 1, now.AddSeconds(1), "unit");
        aggregate.AddVersion(Guid.NewGuid(), 2, Policy(now) with { PolicyVersion = 2, CreatedOnUtc = now.AddSeconds(2) }, now.AddSeconds(2), "unit");
        var activated = (PortfolioFinancialPolicyActivated)aggregate.Activate(Guid.NewGuid(), 3, 2, now.AddSeconds(3), "unit") with
        { EventId = 99, AggregateId = id.Format(), ReceivedOn = now.AddSeconds(3) };
        var writer = new CapturingWriter();

        await new PortfolioProjectionHandler(new PolicyEventStore(id, aggregate), writer).ApplyAsync(activated);

        writer.Policies.Should().HaveCount(2).And.OnlyContain(x => x.SourceEventId == 99 && x.AggregateVersion == 4);
        writer.Policies.OrderBy(x => x.Value.PolicyVersion).Select(x => x.Value.OperatingState).Should().Equal(
            PortfolioFinancialPolicyState.Superseded, PortfolioFinancialPolicyState.Active);
        writer.Policies.Should().OnlyContain(x => x.Value.AggregateRevision == 4);
    }

    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Category", "Portfolio")]
    public async Task Committed_draft_deletion_uses_the_event_fence_and_does_not_upsert_the_Portfolio()
    {
        var now = new DateTime(2026, 8, 30, 18, 0, 0, DateTimeKind.Utc);
        var aggregate = new PortfolioAggregate();
        aggregate.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "unit",
        }, now, "unit");
        var deleted = (DraftPortfolioDeleted)aggregate.DeleteDraft(Guid.NewGuid(), 1, "duplicate", now.AddMinutes(1), "unit");
        deleted = deleted with { EventId = 77, AggregateId = "101", ReceivedOn = now.AddMinutes(1) };
        var events = new DeletionEventStore(aggregate);
        var writer = new CapturingWriter();

        await new PortfolioProjectionHandler(events, writer).ApplyAsync(deleted);

        writer.Deletion.Should().NotBeNull();
        writer.Deletion!.PortfolioId.Should().Be(101);
        writer.Deletion.SourceEventId.Should().Be(77);
        writer.Portfolio.Should().BeNull();
    }

    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Category", "Portfolio")]
    public async Task Committed_reservation_maps_order_trades_and_workflow_with_one_source_event_fence()
    {
        var now = new DateTime(2026, 8, 30, 18, 0, 0, DateTimeKind.Utc);
        var writer = new CapturingWriter();
        var handler = new PortfolioProjectionHandler(null!, writer);
        var reservation = new FundCompositionReservationResult
        {
            Order = new() { PortfolioId = 101, FundId = 202, OrderId = 7001, WorkflowId = Guid.NewGuid(), Status = "TemplateSelected", CreatedOnUtc = now, AggregateVersion = 4 },
            Trades = [new() { PortfolioId = 101, FundId = 202, OrderId = 7001, TradeId = 8001, LegOrdinal = 1, AggregateVersion = 4 }, new() { PortfolioId = 101, FundId = 202, OrderId = 7001, TradeId = 8002, LegOrdinal = 2, AggregateVersion = 4 }],
            AggregateVersion = 4, CommittedOnUtc = now, Disposition = ReservationDisposition.Committed,
        };

        await handler.ApplyCompositionAsync(reservation, 55, now);

        writer.Order!.SourceEventId.Should().Be(55);
        writer.Order.AggregateVersion.Should().Be(4);
        writer.Trades.Should().HaveCount(2).And.OnlyContain(x => x.SourceEventId == 55);
        writer.Composition!.Value.OrderId.Should().Be(7001);
        writer.Month.Should().Be(new DateOnly(2026, 8, 1));
    }

    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Category", "Portfolio")]
    public async Task Uncommitted_projection_is_rejected_before_any_mutation()
    {
        var writer = new CapturingWriter();
        var handler = new PortfolioProjectionHandler(null!, writer);
        var action = () => handler.ApplyCompositionAsync(new FundCompositionReservationResult(), 0, DateTime.UtcNow);

        await action.Should().ThrowAsync<InvalidOperationException>();
        writer.Order.Should().BeNull();
    }

    sealed class CapturingWriter : IPortfolioDbWriteContext
    {
        public PortfolioProjection<FundOrderProjectionReadModel>? Order { get; private set; }
        public PortfolioProjection<PortfolioReadModel>? Portfolio { get; private set; }
        public DraftPortfolioProjectionDeletion? Deletion { get; private set; }
        public List<PortfolioProjection<FundOrderTradeProjectionReadModel>> Trades { get; } = [];
        public List<PortfolioProjection<PortfolioFinancialPolicyReadModel>> Policies { get; } = [];
        public PortfolioProjection<FundCompositionWorkflowProjectionReadModel>? Composition { get; private set; }
        public DateOnly Month { get; private set; }
        public Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row, DateOnly orderMonth, CancellationToken cancellationToken = default) { Order = row; Month = orderMonth; return Task.CompletedTask; }
        public Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row, CancellationToken cancellationToken = default) { Trades.Add(row); return Task.CompletedTask; }
        public Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row, CancellationToken cancellationToken = default) { Composition = row; return Task.CompletedTask; }
        public Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int stateBucket, CancellationToken cancellationToken = default) { Portfolio = row; return Task.CompletedTask; }
        public Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteDraftPortfolioAsync(DraftPortfolioProjectionDeletion deletion, CancellationToken cancellationToken = default) { Deletion = deletion; return Task.CompletedTask; }
        public Task UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel> row, CancellationToken cancellationToken = default) { Policies.Add(row); return Task.CompletedTask; }
    }

    sealed class DeletionEventStore(PortfolioAggregate aggregate) : IPortfolioEventStore
    {
        public Task<PortfolioAggregate> LoadPortfolioAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId id, CancellationToken cancellationToken = default) => Task.FromResult(aggregate);
        public Task<IReadOnlyList<PortfolioFundDomainEvent>> LoadFundHistoryAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortfolioFundDomainEvent>>([]);
        public Task AppendPortfolioAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId id, PortfolioDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AppendFundAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId id, PortfolioFundDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundAggregate> LoadFundAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SavePortfolioSnapshotAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId id, PortfolioAggregate value, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveFundSnapshotAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId id, PortfolioFundAggregate value, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId id, Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundDomainEvent?> FindCommittedFundCommandAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId id, Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioCreated?> FindPortfolioCreateByIdempotencyKeyAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId id, Guid idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FundMandateCreated?> FindFundCreateByIdempotencyKeyAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioFundId id, Guid idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PortfolioDomainEvent>> LoadPortfolioHistoryAsync(TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class PolicyEventStore(PortfolioFinancialPolicyId id, PortfolioFinancialPolicyAggregate aggregate) : IPortfolioEventStore
    {
        public Task<PortfolioFinancialPolicyAggregate> LoadPolicyAsync(PortfolioFinancialPolicyId requested, CancellationToken cancellationToken = default) =>
            Task.FromResult(requested == id ? aggregate : throw new InvalidOperationException());
        public Task AppendPortfolioAsync(PortfolioId id, PortfolioDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AppendFundAsync(PortfolioFundId id, PortfolioFundDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SavePortfolioSnapshotAsync(PortfolioId id, PortfolioAggregate value, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveFundSnapshotAsync(PortfolioFundId id, PortfolioFundAggregate value, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId id, Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId id, Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioCreated?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId id, Guid idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FundMandateCreated?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId id, Guid idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    static PortfolioFinancialPolicyReadModel Policy(DateTime now) => new()
    {
        PortfolioId = 101, PolicyId = 9001, PolicyVersion = 1, Name = "Limits", OperatingState = PortfolioFinancialPolicyState.Draft,
        CapitalBase = 1_000_000, MaximumDeployableCapital = 900_000, MaximumRiskPerTrade = 10_000,
        MaximumAggregateRisk = 100_000, MaximumMargin = 500_000, MaximumGrossNotional = 5_000_000,
        MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000,
        TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }],
        EffectiveFromUtc = now.AddMinutes(-1), CreatedOnUtc = now, CreatedBy = "unit"
    };
}
