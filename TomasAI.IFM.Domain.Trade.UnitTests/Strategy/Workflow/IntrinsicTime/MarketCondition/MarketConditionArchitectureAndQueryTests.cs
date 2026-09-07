using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using System.Collections;
using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionArchitectureAndQueryTests
{
    [Fact]
    public void Function_terminal_map_owns_completion_and_failure_without_factory_overrides()
    {
        var actor=typeof(MarketConditionFunctionActor);
        var map=(System.Collections.IDictionary)actor.GetField("_eventMap",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        map.Keys.Cast<Type>().Should().BeEquivalentTo([typeof(MarketConditionAssessmentCompletedEvent),typeof(MarketConditionAssessmentFailedEvent)]);
        actor.GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Should().NotContain(method=>method.Name=="CreateFailedEvent" || method.Name=="CreateConflictFailedEvent" || method.Name=="WithinDeadlineAsync");
    }

    [Fact]
    public void Function_maps_have_exact_request_set_and_no_legacy_actor_types_exist()
    {
        typeof(IMarketConditionFunctionContext).GetProperties().Select(x => x.Name)
            .Should().Contain(["StateRepository", "FunctionProjector", "SnapshotProvider", "TimeProvider", "Logger"]);
        typeof(MarketConditionFunctionActor).BaseType!.GetGenericTypeDefinition()
            .Should().Be(typeof(BaseEventSourceFunctionActor<,,,,,,>));
        foreach (var name in new[] { "_parseMap", "_validationMap", "_receiveMap" })
        {
            var field = typeof(MarketConditionFunctionActor).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            field.IsInitOnly.Should().BeTrue();
            var map = (IDictionary)field.GetValue(null)!;
            map.IsReadOnly.Should().BeTrue();
            if (name == "_parseMap") map.Keys.Cast<string>().Should().Equal(ExecuteMarketConditionAssessmentCommand.Verb);
            else map.Keys.Cast<Type>().Should().Equal(typeof(ExecuteMarketConditionAssessmentCommand));
        }
        typeof(MarketConditionFunctionActor).Assembly.GetTypes().Select(x => x.Name).Should()
            .NotContain(["MarketConditionCalculationModel", "MarketConditionFunctionState", "MarketConditionFunctionProjector",
                "MarketConditionOptionUniverseAdapter", "MarketConditionOperationalHealthAdapter"]);
        var contracts = typeof(ExecuteMarketConditionPipelineCommand).Assembly;
        contracts.GetType("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands." +
                          "StartMarketConditionPipelineCommand").Should().BeNull();
        contracts.GetType("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events." +
                          "MarketConditionPipelineProcessingEvent").Should().BeNull();
        typeof(MarketConditionFunctionActor).Assembly.GetTypes().Where(type =>
            type.Name == "MarketConditionCommandActor" || type.Name == "MarketConditionEventActor" ||
            type.Name == "MarketConditionRealtimeActor").Should().BeEmpty();
    }

    [Fact]
    public void Query_maps_support_exact_latest_and_bounded_history_with_matching_sets()
    {
        var parse = Map(typeof(MarketConditionQueryActor), "_parseMap");
        var receive = Map(typeof(MarketConditionQueryActor), "_receiveMap");

        parse.Keys.Cast<string>().Should().BeEquivalentTo(
            GetMarketConditionQuery.Verb,
            GetLatestMarketConditionQuery.Verb,
            GetMarketConditionHistoryQuery.Verb,
            Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.GetMarketConditionAssessmentQuery.Verb,
            Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.GetMarketConditionAssessmentHistoryQuery.Verb,
            Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.GetMarketConditionAssessmentReferenceQuery.Verb);
        receive.Keys.Cast<Type>().Should().BeEquivalentTo(new[]
        {
            typeof(GetMarketConditionQuery),
            typeof(GetLatestMarketConditionQuery),
            typeof(GetMarketConditionHistoryQuery),
            typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.GetMarketConditionAssessmentQuery),
            typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.GetMarketConditionAssessmentHistoryQuery),
            typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.GetMarketConditionAssessmentReferenceQuery)
        });
    }

    [Fact]
    public void Latest_and_history_queries_round_trip_append_only_contracts()
    {
        var subject = new ActorSubject(ActorType.Query, GetMarketConditionQuery.Actor,
            GetMarketConditionHistoryQuery.Verb, "1.ES.Daily");
        var expected = new GetMarketConditionHistoryQuery
        {
            Subject = subject,
            EntityId = new ActorEntityId("1.ES.Daily"),
            FundId = 1,
            InstrumentRoot = "ES",
            TargetHorizon = TimeFrameType.Daily,
            BeforeUtc = new DateTime(2026, 8, 28, 16, 0, 0, DateTimeKind.Utc),
            PageSize = 25
        };

        var actual = MessagePackSerializer.Deserialize<GetMarketConditionHistoryQuery>(
            MessagePackSerializer.Serialize(expected));

        actual.Should().BeEquivalentTo(expected);
        actual.EntityId.Format().Should().Be(expected.EntityId.Format());
    }

    static IDictionary Map(Type type, string name) => (IDictionary)type
        .GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
