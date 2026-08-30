using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Projection;

public sealed class PortfolioProjectionHandlerTests
{
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
        public List<PortfolioProjection<FundOrderTradeProjectionReadModel>> Trades { get; } = [];
        public PortfolioProjection<FundCompositionWorkflowProjectionReadModel>? Composition { get; private set; }
        public DateOnly Month { get; private set; }
        public Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row, DateOnly orderMonth, CancellationToken cancellationToken = default) { Order = row; Month = orderMonth; return Task.CompletedTask; }
        public Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row, CancellationToken cancellationToken = default) { Trades.Add(row); return Task.CompletedTask; }
        public Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row, CancellationToken cancellationToken = default) { Composition = row; return Task.CompletedTask; }
        public Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int stateBucket, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
