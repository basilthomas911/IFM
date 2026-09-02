using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookSnapshotCommandContractTests(MarketDataAnalyticsTestFixture fixture)
    : IClassFixture<MarketDataAnalyticsTestFixture>
{
    [Fact]
    public void InsertCommand_RoundTripsTheCompleteSnapshot()
    {
        var snapshot = Snapshot();
        var source = new InsertMarketOutlookSnapshotCommand(snapshot);

        var roundTrip = fixture.DataSerializer.Deserialize<InsertMarketOutlookSnapshotCommand>(
            fixture.DataSerializer.Serialize(source));

        roundTrip.CommandId.Should().Be(source.CommandId);
        roundTrip.Subject.Should().Be(source.Subject);
        roundTrip.EntityId.Should().Be(source.EntityId);
        roundTrip.MarketOutlook.Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void Execute_AppliesOneInsertedEventToEventSourcedState()
    {
        var command = new InsertMarketOutlookSnapshotCommand(Snapshot());
        var state = new MarketOutlookSnapshotCommandState { Id = command.Subject.ThreadId };

        var result = command.Execute(state);

        result.Success.Should().BeTrue();
        state.Updated.Should().BeTrue();
        state.Snapshot.Should().Be(command.MarketOutlook);
        var inserted = state.Events.Should().ContainSingle().Subject
            .Should().BeOfType<MarketOutlookSnapshotInsertedEvent>().Subject;
        inserted.CommandId.Should().Be(command.CommandId);
        inserted.EntityId.Should().Be(command.EntityId);
        inserted.MarketOutlook.Should().Be(command.MarketOutlook);
    }

    [Fact]
    public void UnsupportedStateEvent_DoesNotMutateTheSnapshot()
    {
        var command = new InsertMarketOutlookSnapshotCommand(Snapshot());
        var state = new MarketOutlookSnapshotCommandState { Id = command.Subject.ThreadId };
        command.Execute(state).Success.Should().BeTrue();
        var expected = state.Snapshot;

        state.Update(Substitute.For<IEvent>(), addEvent: false)
            .Should().BeFalse();

        state.Snapshot.Should().BeSameAs(expected);
    }

    [Fact]
    public void SnapshotContractCatalog_HasNoCompleteOrFailedLifecycleEvents()
    {
        var contracts = typeof(MarketOutlookSnapshotInsertedEvent).Assembly.GetTypes()
            .Select(type => type.Name)
            .ToArray();

        contracts.Should().NotContain("MarketOutlookSnapshotInsertedCompleteEvent");
        contracts.Should().NotContain("MarketOutlookSnapshotInsertedFailEvent");
    }

    [Fact]
    public void InsertedEvent_RejectsAZeroPriceSnapshotAtTheUiBoundary()
    {
        var snapshot = Snapshot();
        var id = new MarketOutlookEntityId(snapshot.ContractId, snapshot.ValueDate);
        var inserted = new MarketOutlookSnapshotInsertedEvent
        {
            CommandId = Guid.NewGuid(),
            EntityId = id,
            MarketOutlook = snapshot with
            {
                FuturesEodData = snapshot.FuturesEodData with { OpenPrice = 0m }
            }
        };

        inserted.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CommandWriter_UsesTheSourceUpdateIdAsTheAuditedCommandId()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var producer = Substitute.For<IActorProducer>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        InsertMarketOutlookSnapshotCommand? captured = null;
        producer.RequestAsync<InsertMarketOutlookSnapshotCommand, MarketOutlookEntityId, GuidResult>(
                Arg.Any<ActorSubject>(),
                Arg.Do<InsertMarketOutlookSnapshotCommand>(command => captured = command),
                Arg.Any<MarketOutlookEntityId>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new(Guid.NewGuid()))));
        var writer = new ActorMarketOutlookSnapshotCommandWriter(supervisor);
        var updateId = Guid.NewGuid();
        var snapshot = Snapshot();
        var update = new EodMarketOutlookUpdate
        {
            UpdateId = updateId,
            EntityId = new(snapshot.ContractId, snapshot.ValueDate),
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = snapshot.MarketDataAsOfUtc,
            Eod = snapshot.FuturesEodData
        };

        await writer.PublishAsync(update, snapshot, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.CommandId.Should().Be(updateId);
        captured.MarketOutlook.Should().Be(snapshot);
        captured.Subject.ActorType.Should().Be(ActorType.Command);
    }

    [Fact]
    public async Task CommandWriter_StampsTheConfiguredFeedSourceOnTheDurableSnapshot()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var producer = Substitute.For<IActorProducer>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        InsertMarketOutlookSnapshotCommand? captured = null;
        producer.RequestAsync<InsertMarketOutlookSnapshotCommand, MarketOutlookEntityId, GuidResult>(
                Arg.Any<ActorSubject>(),
                Arg.Do<InsertMarketOutlookSnapshotCommand>(command => captured = command),
                Arg.Any<MarketOutlookEntityId>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new(Guid.NewGuid()))));
        var writer = new ActorMarketOutlookSnapshotCommandWriter(
            supervisor,
            new(MarketOutlookSnapshotSource.Synthetic));
        var snapshot = Snapshot();
        var update = new EodMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = new(snapshot.ContractId, snapshot.ValueDate),
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = snapshot.MarketDataAsOfUtc,
            Eod = snapshot.FuturesEodData
        };

        await writer.PublishAsync(update, snapshot, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.MarketOutlook.SnapshotSource.Should()
            .Be(MarketOutlookSnapshotSource.Synthetic);
        snapshot.SnapshotSource.Should().Be(MarketOutlookSnapshotSource.Unknown);
    }

    [Fact]
    public void PersistabilityValidation_AcceptsValidEodWithOptionalAnalyticsMissing()
    {
        var command = new InsertMarketOutlookSnapshotCommand(Snapshot() with
        {
            FuturesTradeSignal = null
        });

        new List<ValidationError>().ValidateSnapshot(command).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 110, 90, 105, 1)]
    [InlineData(100, 0, 90, 105, 1)]
    [InlineData(100, 110, 0, 105, 1)]
    [InlineData(100, 110, 90, 0, 1)]
    [InlineData(100, 90, 110, 100, 1)]
    [InlineData(80, 110, 90, 100, 1)]
    [InlineData(100, 110, 90, 120, 1)]
    [InlineData(100, 110, 90, 105, -1)]
    public void PersistabilityValidation_RejectsEveryInvalidOhlcInvariant(
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        long volume)
    {
        var snapshot = Snapshot();
        var command = new InsertMarketOutlookSnapshotCommand(snapshot with
        {
            FuturesEodData = snapshot.FuturesEodData with
            {
                OpenPrice = open,
                HighPrice = high,
                LowPrice = low,
                ClosePrice = close,
                Volume = volume
            }
        });

        new List<ValidationError>().ValidateSnapshot(command).Should().NotBeEmpty();
    }

    [Fact]
    public void PersistabilityValidation_RejectsPayloadIdentityMismatch()
    {
        var snapshot = Snapshot();
        var command = new InsertMarketOutlookSnapshotCommand(snapshot with
        {
            ContractId = "NQZ26"
        });

        new List<ValidationError>().ValidateSnapshot(command).Should().NotBeEmpty();
    }

    static MarketOutlookReadModel Snapshot()
    {
        var eod = SampleData.EodData with
        {
            Symbol = "ES",
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        return new()
        {
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate,
            UpdatedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = DateTime.UtcNow,
            FuturesEodData = eod,
            FuturesTradeSignal = SampleData.TradeSignalReadModelFor(TimeFrameType.Daily)
        };
    }
}
