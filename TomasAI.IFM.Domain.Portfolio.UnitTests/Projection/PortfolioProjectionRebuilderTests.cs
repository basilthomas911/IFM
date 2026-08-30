using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Projection;

public sealed class PortfolioProjectionRebuilderTests
{
    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Category", "Portfolio")]
    public async Task Failed_target_mutation_cannot_return_success_and_retry_replays_the_same_authoritative_event()
    {
        var now = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);
        var id = new PortfolioId(101);
        var aggregate = new PortfolioAggregate();
        var committed = (PortfolioCreated)aggregate.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = id.Id, PortfolioCode = "CORE", Name = "Core", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "test",
        }, now, "test") with { EventId = 55, AggregateId = id.Format(), ReceivedOn = now };
        var events = new StubEventStore(id, aggregate, committed);
        var writer = new FailOnceWriter();
        var rebuilder = new PortfolioProjectionRebuilder(events, writer);
        var request = new PortfolioProjectionRebuildRequest([id], []);

        var failed = () => rebuilder.RebuildAsync(request);
        await failed.Should().ThrowAsync<InvalidOperationException>().WithMessage("target mutation failed");
        writer.SuccessfulMutations.Should().Be(0);

        var retry = await rebuilder.RebuildAsync(request);
        retry.EventCount.Should().Be(1);
        retry.LastSourceEventId.Should().Be(55);
        retry.SourceCatalogSha256.Should().HaveLength(64);
        writer.SuccessfulMutations.Should().Be(1);
    }

    sealed class StubEventStore(
        PortfolioId portfolioId,
        PortfolioAggregate aggregate,
        PortfolioDomainEvent committed) : IPortfolioEventStore
    {
        public Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == portfolioId ? aggregate : throw new InvalidOperationException($"Unexpected portfolio {id}."));

        public Task<IReadOnlyList<PortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PortfolioDomainEvent>>(id == portfolioId ? [committed] : []);

        public Task AppendPortfolioAsync(PortfolioId id, PortfolioDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AppendFundAsync(PortfolioFundId id, PortfolioFundDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SavePortfolioSnapshotAsync(PortfolioId id, PortfolioAggregate value, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveFundSnapshotAsync(PortfolioFundId id, PortfolioFundAggregate value, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId id, Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId id, Guid commandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioCreated?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId id, Guid idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FundMandateCreated?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId id, Guid idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortfolioFundDomainEvent>>([]);
    }

    sealed class FailOnceWriter : IPortfolioDbWriteContext
    {
        int _attempt;
        public int SuccessfulMutations { get; private set; }
        public Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int stateBucket, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _attempt) == 1) throw new InvalidOperationException("target mutation failed");
            SuccessfulMutations++; return Task.CompletedTask;
        }
        public Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row, DateOnly orderMonth, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
