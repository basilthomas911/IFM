using System;
using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
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
        var reader = Substitute.For<IObjectMapReader<EventProjectorStateReadModel>>();
        reader.Get(Arg.Any<Expression<Func<EventProjectorStateReadModel, long>>>()).Returns(101L);
        reader.Get(Arg.Any<Expression<Func<EventProjectorStateReadModel, string>>>()).Returns(call =>
        {
            var propertyName = ((MemberExpression)call.Arg<Expression<Func<EventProjectorStateReadModel, string>>>().Body).Member.Name;
            return propertyName switch
            {
                nameof(EventProjectorStateReadModel.ActorName) => "FundCommandActor",
                nameof(EventProjectorStateReadModel.ProjectorName) => "FundEventProjector",
                nameof(EventProjectorStateReadModel.ErrorMessage) => "retry",
                _ => string.Empty
            };
        });
        reader.Get(Arg.Any<Expression<Func<EventProjectorStateReadModel, bool>>>()).Returns(true);
        reader.Get(Arg.Any<Expression<Func<EventProjectorStateReadModel, int>>>()).Returns(2);
        reader.Get<EventProjectorOutcomeType>(Arg.Any<Expression<Func<EventProjectorStateReadModel, EventProjectorOutcomeType>>>())
            .Returns(EventProjectorOutcomeType.Retrying);
        reader.Get<EventProjectorStageType>(Arg.Any<Expression<Func<EventProjectorStateReadModel, EventProjectorStageType>>>())
            .Returns(EventProjectorStageType.ApplyProjection);
        reader.GetISODateTime(Arg.Any<Expression<Func<EventProjectorStateReadModel, DateTime>>>())
            .Returns(created, updated);

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
        EventSourceDbSql.CreateEventProjectorState.Should()
            .Contain("CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_event_version")
            .And.Contain("ON event_log (EventVersion)")
            .And.Contain("PRIMARY KEY (EventId, ProjectorName)");
        EventSourceDbSql.GetUncompletedEventProjectorEvents.Should()
            .Contain("eps.ProjectorName = $1")
            .And.Contain("eps.Outcome IN ('Processing', 'Retrying')")
            .And.NotContain("eps.EventId IS NULL");
        EventSourceDbSql.GetEventNameId.Should()
            .Contain("e.eventName = $1")
            .And.Contain("e.eventTypeName = $2");
    }
}
