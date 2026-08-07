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
    }
}
