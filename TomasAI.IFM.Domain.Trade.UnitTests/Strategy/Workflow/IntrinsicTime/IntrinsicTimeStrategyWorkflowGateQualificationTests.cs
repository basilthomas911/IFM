using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Qualifies ITSW-7 through ITSW-11 structural boundaries.</summary>
public sealed class IntrinsicTimeStrategyWorkflowGateQualificationTests
{
    /// <summary>Confirms cache writes are monotonic by workflow revision.</summary>
    [Fact]
    public void Projection_cache_rejects_older_revision()
    {
        var cache = new IntrinsicTimeStrategyWorkflowProjectionCache();
        var newer = Active(4);
        cache.Set(newer);
        cache.Set(Active(3));

        cache.TryGet(newer.WorkflowEntityId, out var actual).Should().BeTrue();
        actual!.WorkflowRevision.Should().Be(4);
    }

    /// <summary>Confirms live automatic triggering defaults to disabled.</summary>
    [Fact]
    public void Live_workflow_feature_defaults_to_disabled()
        => new IntrinsicTimeStrategyWorkflowOptions().Enabled.Should().BeFalse();

    /// <summary>Confirms ITSW-9 introduces no durable workflow Event actor.</summary>
    [Fact]
    public void Workflow_assembly_contains_no_durable_event_actor()
    {
        var eventActors = typeof(IntrinsicTimeStrategyWorkflowRealtimeActor).Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains("Strategy.Workflow.IntrinsicTime", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("EventActor", StringComparison.Ordinal))
            .ToArray();

        eventActors.Should().BeEmpty();
    }

    /// <summary>Confirms only external sources use global realtime route registration.</summary>
    [Fact]
    public void Realtime_actor_declares_eleven_external_progression_routes()
    {
        var routes = (ActorTypeId[])typeof(IntrinsicTimeStrategyWorkflowRealtimeActor)
            .GetField("ExternalRoutes", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        routes.Should().HaveCount(11);
        routes.Should().OnlyContain(route => route.ActorType == ActorType.Realtime);
        routes.Should().NotContain(route => route.Name == "IntrinsicTimeStrategyWorkflow");
    }

    /// <summary>Confirms the typed query contract survives the default MessagePack transport boundary.</summary>
    [Fact]
    public void Workflow_query_contract_round_trips_through_messagepack()
    {
        var expected = new GetActiveIntrinsicTimeStrategyWorkflowQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetActiveIntrinsicTimeStrategyWorkflowQuery.Actor,
                GetActiveIntrinsicTimeStrategyWorkflowQuery.Verb, "IntrinsicTimeStrategy.ES.20260825.Daily"),
            EntityId = new ActorEntityId("IntrinsicTimeStrategy.ES.20260825.Daily"),
            WorkflowEntityId = "IntrinsicTimeStrategy.ES.20260825.Daily",
            MinimumWorkflowRevision = 7
        };

        var actual = MessagePackSerializer.Deserialize<GetActiveIntrinsicTimeStrategyWorkflowQuery>(
            MessagePackSerializer.Serialize(expected));

        actual.Subject.Should().Be(expected.Subject);
        actual.EntityId.Format().Should().Be(expected.EntityId.Format());
        actual.WorkflowEntityId.Should().Be(expected.WorkflowEntityId);
        actual.MinimumWorkflowRevision.Should().Be(7);
    }

    static ActiveIntrinsicTimeStrategyWorkflowReadModel Active(long revision)
        => new("IntrinsicTimeStrategy.ES.20260825.Daily", default, "ES", new DateOnly(2026, 8, 25),
            Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,
            Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowStage.RegimeDiscovery,
            revision, revision, 1, ReadOnlyMemory<byte>.Empty,
            new DateTime(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 25, 18, 1, 0, DateTimeKind.Utc));
}
