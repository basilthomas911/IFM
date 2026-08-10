using System;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class EventProjectorStatePersistenceTests
{
    [Fact]
    public void MapToEventProjectorState_maps_composite_identity_and_resume_state()
    {
        var created = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
        var updated = created.AddMinutes(1);
        var reader = Substitute.For<IObjectDataRecord>();
        reader.GetLong(0).Returns(101L);
        reader.GetString(1).Returns("FundCommandActor");
        reader.GetString(2).Returns("FundEventProjector");
        reader.GetBool(3).Returns(true);
        reader.GetInt(4).Returns(2);
        reader.GetEnum<EventProjectorOutcomeType>(5).Returns(EventProjectorOutcomeType.Retrying);
        reader.GetEnum<EventProjectorStageType>(6).Returns(EventProjectorStageType.ApplyProjection);
        reader.GetString(7).Returns("retry");
        reader.GetDateTime(8).Returns(created);
        reader.GetDateTime(9).Returns(updated);

        var state = EventSourceActorDbContext.MapToEventProjectorState(reader);

        state.EventId.Should().Be(101L);
        state.ActorName.Should().Be("FundCommandActor");
        state.ProjectorName.Should().Be("FundEventProjector");
        state.IsReplay.Should().BeTrue();
        state.AttemptNumber.Should().Be(2);
        state.Outcome.Should().Be(EventProjectorOutcomeType.Retrying);
        state.Stage.Should().Be(EventProjectorStageType.ApplyProjection);
        state.ErrorMessage.Should().Be("retry");
        state.CreatedTimestamp.Should().Be(created);
        state.UpdatedTimestamp.Should().Be(updated);
    }

    [Fact]
    public void MapToEventProjectorExecutionState_maps_lease_revision_and_retry_metadata()
    {
        var created = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var token = Guid.NewGuid();
        var lease = created.AddMinutes(2);
        var retryAt = created.AddMinutes(3);
        var reader = Substitute.For<IObjectDataRecord>();
        reader.GetLong(0).Returns(101L);
        reader.GetString(1).Returns("FundCommandActor");
        reader.GetString(2).Returns("FundEventProjector");
        reader.GetBool(3).Returns(true);
        reader.GetInt(4).Returns(2);
        reader.GetEnum<EventProjectorOutcomeType>(5).Returns(EventProjectorOutcomeType.Retrying);
        reader.GetEnum<EventProjectorStageType>(6).Returns(EventProjectorStageType.ApplyProjection);
        reader.GetString(7).Returns("retry");
        reader.GetDateTime(8).Returns(created);
        reader.GetDateTime(9).Returns(created.AddMinutes(1));
        reader.GetLong(10).Returns(44L);
        reader.GetString(11).Returns("FundCreatedEvent");
        reader.GetLong(12).Returns(7L);
        reader.IsNull(13).Returns(false);
        reader.GetGuid(13).Returns(token);
        reader.IsNull(14).Returns(false);
        reader.GetDateTime(14).Returns(lease);
        reader.GetInt(15).Returns(1);
        reader.IsNull(16).Returns(false);
        reader.GetDateTime(16).Returns(retryAt);
        reader.IsNull(17).Returns(true);
        reader.GetString(18).Returns("waiting");
        reader.GetEnum<EventProjectorStageType>(19).Returns(EventProjectorStageType.PublishProcessingEvent);
        reader.GetDateTime(20).Returns(created.AddMinutes(1));

        var state = EventSourceActorDbContext.MapToEventProjectorExecutionState(reader);

        state.EventId.Should().Be(101L);
        state.EventStreamId.Should().Be(44L);
        state.SourceEventName.Should().Be("FundCreatedEvent");
        state.Revision.Should().Be(7L);
        state.ExecutionToken.Should().Be(token);
        state.LeaseExpiresAtUtc.Should().Be(lease);
        state.RetryCount.Should().Be(1);
        state.NextAttemptAtUtc.Should().Be(retryAt);
        state.LastErrorAtUtc.Should().BeNull();
        state.BlockedReason.Should().Be("waiting");
        state.LastCompletedStage.Should().Be(EventProjectorStageType.PublishProcessingEvent);
    }

    [Fact]
    public void Projector_state_schema_uses_event_and_projector_as_composite_key()
    {
        EventSourceSchemaSql.CreateEventProjectorState.Should()
            .Contain("CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_event_version")
            .And.Contain("ON event_log (EventVersion)")
            .And.Contain("PRIMARY KEY (EventId, ProjectorName)");
        EventSourceDbSql.GetUncompletedEventProjectorEvents.Should()
            .Contain("eps.ProjectorName = $1")
            .And.Contain("eps.Outcome IN ('Processing', 'Retrying')")
            .And.NotContain("eps.EventId IS NULL");
        EventSourceSchemaSql.CreateEventLogTable.Should()
            .Contain("ix_event_log_command_id")
            .And.Contain("(CommandId)");
        EventSourceDbSql.GetEventNameId.Should()
            .Contain("e.eventName = $1")
            .And.Contain("e.eventTypeName = $2");
        EventSourceSchemaSql.CreateEventProjectorStateReliabilityV2.Should()
            .Contain("Revision bigint NOT NULL DEFAULT 0")
            .And.Contain("ExecutionToken uuid")
            .And.Contain("LeaseExpiresAtUtc timestamptz")
            .And.Contain("ix_event_projector_state_pending_v2")
            .And.Contain("ix_event_projector_state_stream_pending_v2");
        EventSourceSchemaSql.CreateEventProjectorOutboxV2.Should()
            .Contain("PRIMARY KEY (ProjectorName, EventId, EffectKind)")
            .And.Contain("MessageId varchar(128) NOT NULL")
            .And.Contain("UNIQUE (MessageId)")
            .And.Contain("ix_event_projector_outbox_pending");
        EventSourceDbSql.TryClaimEventProjectorExecution.Should()
            .Contain("Revision = Revision + 1")
            .And.Contain("LeaseExpiresAtUtc <= $5");
        EventSourceDbSql.TryTransitionEventProjectorExecution.Should()
            .Contain("ExecutionToken = $3")
            .And.Contain("Revision = $4")
            .And.Contain("Stage = $5");
        EventSourceDbSql.TryReleaseEventProjectorExecution.Should()
            .Contain("ExecutionToken = NULL")
            .And.Contain("LeaseExpiresAtUtc = NULL")
            .And.Contain("Outcome = 'Retrying'")
            .And.Contain("Revision = $4")
            .And.Contain("Stage = $5");
        EventSourceDbSql.GetEventProjectorRecoveryPage.Should()
            .Contain("eps.EventId > $3")
            .And.Contain("eps.LeaseExpiresAtUtc <= $4")
            .And.Contain("ORDER BY eps.EventId")
            .And.Contain("LIMIT $5");
    }
}
