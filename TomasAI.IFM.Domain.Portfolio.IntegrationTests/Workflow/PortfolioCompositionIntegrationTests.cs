using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Portfolio.Command;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using NSubstitute;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Workflow;

public sealed class PortfolioCompositionIntegrationTests
{
    static readonly DateTime Now = new(2026, 8, 30, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-12")]
    [Trait("Category", "Portfolio")]
    public async Task Allocation_service_retains_integer_ids_and_does_not_reallocate_an_idempotent_replay()
    {
        var allocator = new CountingAllocator();
        var aggregate = new PortfolioFundCompositionAggregate();
        var service = new PortfolioFundCompositionService(allocator, aggregate);
        var (request, snapshot) = ValidRequest();

        var first = await service.ReserveAsync(request, snapshot, Now, "integration");
        var replay = await service.ReserveAsync(request, snapshot, Now.AddSeconds(1), "integration");

        first.Order.OrderId.Should().Be(7001);
        first.Trades.Select(x => x.TradeId).Should().Equal(8001, 8002, 8003, 8004);
        replay.Order.OrderId.Should().Be(first.Order.OrderId);
        allocator.OrderAllocations.Should().Be(1);
        allocator.TradeAllocations.Should().Be(4);
    }

    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-12")]
    [Trait("Category", "Portfolio")]
    public void Reservation_and_frozen_snapshot_round_trip_through_production_messagepack_contracts()
    {
        var (request, snapshot) = ValidRequest();

        var requestCopy = MessagePackSerializer.Deserialize<ReserveFundOrderCompositionRequest>(MessagePackSerializer.Serialize(request));
        var snapshotCopy = MessagePackSerializer.Deserialize<PortfolioFundStrategySnapshot>(MessagePackSerializer.Serialize(snapshot));

        requestCopy.Should().BeEquivalentTo(request);
        snapshotCopy.Should().BeEquivalentTo(snapshot);
        snapshotCopy.PayloadSha256.Should().Be(snapshot.PayloadSha256);
    }

    [Fact]
    [Trait("Gate", "PF-07")]
    [Trait("Gate", "PF-12")]
    [Trait("Category", "Portfolio")]
    public async Task Authoritative_handler_appends_once_and_replay_does_not_allocate_new_ids()
    {
        var (request, snapshot) = ValidRequest();
        var aggregate = new PortfolioFundAggregate();
        aggregate.RestoreSnapshot(new PortfolioFundAggregateSnapshot(1, snapshot.Fund, snapshot.Assignments, [], [Guid.NewGuid()]));
        var eventStore = Substitute.For<IPortfolioEventStore>();
        eventStore.LoadFundAsync(Arg.Any<PortfolioFundId>(), Arg.Any<CancellationToken>()).Returns(aggregate);
        eventStore.FindCommittedFundCommandAsync(Arg.Any<PortfolioFundId>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PortfolioFundDomainEvent?)null);
        var allocator = new CountingAllocator();
        var handler = new PortfolioFundCompositionCommandHandler(eventStore, allocator);

        var first = await handler.ReserveAsync(Guid.NewGuid(), request, snapshot, Now, "workflow");
        var replay = await handler.ReserveAsync(Guid.NewGuid(), request, snapshot, Now.AddSeconds(1), "workflow");

        first.Order.OrderId.Should().Be(7001);
        replay.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);
        replay.Order.OrderId.Should().Be(first.Order.OrderId);
        allocator.OrderAllocations.Should().Be(1);
        allocator.TradeAllocations.Should().Be(4);
        await eventStore.Received(1).AppendFundAsync(
            new PortfolioFundId(101, 203), Arg.Is<PortfolioFundDomainEvent>(e => e is FundCompositionReserved), 1,
            null, Arg.Any<CancellationToken>());
    }

    static (ReserveFundOrderCompositionRequest, PortfolioFundStrategySnapshot) ValidRequest()
    {
        var templateId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var snapshot = new PortfolioFundStrategySnapshot
        {
            WorkflowId = Guid.NewGuid(), WorkflowRevision = 1, CorrelationId = Guid.NewGuid(),
            Portfolio = new() { PortfolioId = 101, PortfolioVersion = 2, PortfolioCode = "CORE", Name = "Core", OperatingState = PortfolioOperatingState.Active, EffectiveFromUtc = Now.AddDays(-1), PolicyId = Guid.NewGuid(), PolicyVersion = 1, CreatedOnUtc = Now.AddDays(-1), CreatedBy = "admin" },
            Fund = new() { PortfolioId = 101, FundId = 203, FundMandateVersion = 3, FundCode = "MONTHLY", Name = "Monthly", TradingYear = 2026, OperatingState = FundOperatingState.Active, EffectiveFromUtc = Now.AddDays(-1), DecisionHorizon = "Monthly", Objective = "ES", UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["FuturesOptions"], PermittedTradeFamilies = ["IronCondor"], CreatedOnUtc = Now.AddDays(-1), CreatedBy = "admin" },
            Allocation = new() { PortfolioId = 101, PortfolioVersion = 2, FundId = 203, FundMandateVersion = 3, AllocationVersion = 1 },
            RiskEnvelope = new() { PortfolioId = 101, PortfolioVersion = 2, FundId = 203, FundMandateVersion = 3, EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available, EffectiveFromUtc = Now.AddHours(-1), ExpiresAtUtc = Now.AddHours(1) },
            Assignments = [new() { PortfolioId = 101, PortfolioVersion = 2, FundId = 203, FundMandateVersion = 3, AssignmentVersion = 1, TradeTemplateId = templateId, TradeTemplateVersion = 1, Enabled = true, DecisionHorizon = "Monthly", UnderlyingUniverse = ["ES"], AssetType = "FuturesOptions", TradeFamily = "IronCondor", EffectiveFromUtc = Now.AddHours(-1), TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1, OrderCompositionProfileId = profileId, OrderCompositionProfileVersion = 1, CreatedOnUtc = Now.AddHours(-1), CreatedBy = "admin" }],
            ResolvedAtUtc = Now, ValidUntilUtc = Now.AddHours(1),
        };
        snapshot = snapshot with { PayloadSha256 = PortfolioCanonicalHash.Compute(snapshot) };
        var legs = Enumerable.Range(1, 4).Select(i => new TradeInstruction { TradeFamily = "IronCondor", TradeRole = i == 1 ? "Primary" : "Related", DirectionOrBias = "Neutral", TradeAction = i % 2 == 0 ? "Sell" : "Buy", IsPrimaryTrade = i == 1, UnderlyingRoot = "ES", RequestedTradeDate = DateOnly.FromDateTime(Now), Reference = $"leg-{i}", CreatedOnUtc = Now, CreatedBy = "integration" }).ToArray();
        return (new()
        {
            WorkflowId = snapshot.WorkflowId, WorkflowRevision = 1, TradeSelectionInvocationId = Guid.NewGuid(), TradeSelectionResultId = Guid.NewGuid(), TradeSelectionResultSha256 = new string('a', 64),
            PortfolioId = 101, PortfolioVersion = 2, FundId = 203, FundMandateVersion = 3, TradeTemplateId = templateId, TradeTemplateVersion = 1,
            OrderCompositionProfileId = profileId, OrderCompositionProfileVersion = 1, UnderlyingRoot = "ES", DecisionHorizon = "Monthly",
            RequestedTradeDate = DateOnly.FromDateTime(Now), TradeInstructions = legs, Origin = CompositionOrigin.StrategyWorkflow,
            IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = Now, ExpiresAtUtc = Now.AddMinutes(10), PortfolioFundStrategySnapshotSha256 = snapshot.PayloadSha256,
        }, snapshot);
    }

    sealed class CountingAllocator : IPortfolioBusinessIdAllocator
    {
        public int OrderAllocations { get; private set; }
        public int TradeAllocations { get; private set; }
        public ValueTask<PortfolioId> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new PortfolioId(101));
        public ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(203);
        public ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default) { OrderAllocations++; return ValueTask.FromResult(7000 + OrderAllocations); }
        public ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default) { TradeAllocations++; return ValueTask.FromResult(8000 + TradeAllocations); }
    }
}
