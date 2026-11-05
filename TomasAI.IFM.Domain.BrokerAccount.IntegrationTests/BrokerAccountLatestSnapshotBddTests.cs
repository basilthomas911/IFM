using TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Command;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Domain.BrokerAccount.IntegrationTests;

[Collection(nameof(BrokerAccountPostgresCollection))]
public sealed class BrokerAccountLatestSnapshotBddTests(EventSourceActorSnapshotRangeFixture fixture)
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(256, false, false)]
    [InlineData(256, false, true)]
    [InlineData(256, true, false)]
    public async Task Given_account_history_when_loading_then_latest_snapshot_matches_full_replay_and_allows_append(int count, bool mixed, bool historicalIdentity)
    {
        var account = new BrokerAccountId($"test-{Guid.NewGuid():N}");
        var now = DateTime.UtcNow;
        var command = new RecordBrokerAccountSnapshotCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = account,
            Subject = new(ActorType.Command, BrokerAccountActorNames.Command, RecordBrokerAccountSnapshotCommand.Verb, account.Format()),
            Environment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment.Emulator,
            Snapshot = new BrokerAccountSnapshotEvidence { AccountAlias = account.AccountAlias, Currency = "USD", Generation = count + 1, AsOfUtc = now }
        };
        var events = Enumerable.Range(1, count).Select(revision => (IEvent)new BrokerAccountChangedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = account,
            Subject = new(ActorType.Event, BrokerAccountChangedEvent.Actor, BrokerAccountChangedEvent.Verb, account.Format()),
            BrokerAccountDefinition = new BrokerAccountDefinition
            {
                Id = account,
                Environment = command.Environment,
                Revision = revision,
                ChangedAtUtc = now.AddSeconds(revision - count),
                QualificationStatus = revision % 2 == 0 ? BrokerAccountQualificationStatus.Accepted : BrokerAccountQualificationStatus.Revoked,
                ManualHold = revision % 2 == 0
            }
        }).ToArray();
        if (mixed)
        {
            var latest = (BrokerAccountChangedEvent)events[^1];
            events[^1] = new BrokerAccountOperationAppliedEvent
            {
                Id = latest.Id,
                CommandId = latest.CommandId,
                EntityId = account,
                Subject = new(ActorType.Event, BrokerAccountChangedEvent.Actor, BrokerAccountOperationAppliedEvent.Verb, account.Format()),
                BrokerAccountDefinition = latest.BrokerAccountDefinition,
                Operation = BrokerAccountOperation.SetManualTradingHold
            };
        }
        if (mixed)
        {
            events =
            [
                ..events,
                new BrokerAccountOperationDecidedEvent
                {
                    Id = Guid.NewGuid(),
                    CommandId = Guid.NewGuid(),
                    EntityId = account,
                    Subject = new(ActorType.Event, BrokerAccountChangedEvent.Actor, BrokerAccountOperationDecidedEvent.Verb, account.Format()),
                    Accepted = false,
                    RejectionCode = 25104,
                    RejectionReason = "qualification rejected",
                    Operation = BrokerAccountOperation.AcceptAccountQualification
                }
            ];
        }
        if (events.Length != 0)
            await fixture.ActorEventDb.SaveEventsAsync(command.StreamId, Guid.NewGuid(),
                new DomainEventCollection(events), 0, CancellationToken.None);

        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<BrokerAccountCommandState>().Returns(_ => new BrokerAccountCommandState());
        var projector = Substitute.For<IEventProjector<BrokerAccountCommandActor>>();
        var repository = new BrokerAccountStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
            projector, NullLogger<BrokerAccountStateRepository>.Instance);

        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        if (historicalIdentity)
        {
            const string alias = "TomasAI.IFM.Domain.BrokerAccount.Contracts.BrokerAccountChangedEvent, TomasAI.IFM.Domain.BrokerAccount.Shared";
            EventTypeRegistry.Default.RegisterHistoricalIdentity(typeof(BrokerAccountChangedEvent), alias);
            await using var connection = new NpgsqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();
            await using var rewrite = new NpgsqlCommand("""
                WITH alias AS (
                    INSERT INTO event_name_id (EventName, EventTypeName) VALUES ($1, $2)
                    ON CONFLICT (EventName, EventTypeName) DO UPDATE SET EventName = EXCLUDED.EventName
                    RETURNING EventNameId
                )
                UPDATE event_log SET EventNameId = (SELECT EventNameId FROM alias)
                WHERE EventStreamId = $3 AND StreamVersion = $4
                """, connection);
            rewrite.Parameters.AddWithValue(nameof(BrokerAccountChangedEvent));
            rewrite.Parameters.AddWithValue(alias);
            rewrite.Parameters.AddWithValue(streamId);
            rewrite.Parameters.AddWithValue((long)count);
            Assert.Equal(1, await rewrite.ExecuteNonQueryAsync());
        }

        var fullReplay = new BrokerAccountCommandState();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<BrokerAccountCommandState>(
            streamId, fullReplay.ReplayEvents, CancellationToken.None);
        var rowsRead = 0;
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<BrokerAccountCommandState>(
            streamId, [typeof(BrokerAccountChangedEvent), typeof(BrokerAccountOperationAppliedEvent)], rows => rowsRead = rows.Count(), CancellationToken.None);

        var restored = await repository.LoadStateAsync(command);
        Assert.Equal(Math.Min(count, 2) + (mixed ? 1 : 0), rowsRead);
        Assert.Equal(fullReplay.BrokerAccountDefinition, restored.BrokerAccountDefinition);
        Assert.Equal(fullReplay.CommittedStreamVersion, restored.CommittedStreamVersion);
        Assert.Equal((events.Take(count).LastOrDefault() as BrokerAccountChangedEvent)?.BrokerAccountDefinition, restored.BrokerAccountDefinition);
        Assert.Equal(events.Length, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events);

        Assert.True(command.Execute(restored).Success);
        await repository.SaveStateAsync(Substitute.For<ICommandActorContext>(), restored, command);
        var reloaded = await repository.LoadStateAsync(command);
        Assert.Equal(events.Length + 1, reloaded.CommittedStreamVersion);
        Assert.Equal(count + 1, reloaded.BrokerAccountDefinition!.Revision);
    }
}
