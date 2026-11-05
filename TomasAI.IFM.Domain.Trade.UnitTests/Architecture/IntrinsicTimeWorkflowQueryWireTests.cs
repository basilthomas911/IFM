using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Architecture;

/// <summary>Guards the published IntrinsicTime workflow query wire layouts.</summary>
public sealed class IntrinsicTimeWorkflowQueryWireTests
{
    [Fact]
    public void Workflow_detail_batch_returns_a_small_retryable_error_before_exceeding_Nats_limit()
    {
        var payload = new byte[540 * 1024];
        new Random(42).NextBytes(payload);
        var detail = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels.IntrinsicTimeStrategyWorkflowReadModel(
            new(Guid.NewGuid()), "entity", "definition", 1, "ES", new DateOnly(2026, 11, 4),
            default, Guid.NewGuid(), Guid.NewGuid(), default, default, default, 1, 1, 1,
            payload, "", DateTime.UnixEpoch, null, DateTime.UnixEpoch);
        var second = (byte[])payload.Clone();
        new Random(43).NextBytes(second);
        var bounded = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model.IntrinsicTimeStrategyWorkflowQueryModel.BoundWorkflowBatch(
            [detail, detail with { StatePayload = second }]);
        bounded.Success.Should().BeFalse();
        bounded.ErrorCode.Should().Be(413);
        var single = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model.IntrinsicTimeStrategyWorkflowQueryModel.BoundWorkflowBatch([detail]);
        single.Success.Should().BeTrue();
        single.Value.Should().ContainSingle().Which.Should().BeSameAs(detail);
    }

    [Fact]
    public void Grid_page_omits_full_stage_results()
    {
        var view = new IntrinsicTimeStrategyWorkflowView
        {
            WorkflowId = new(Guid.NewGuid()),
            WorkflowRevision = 7,
            StartedAtUtc = DateTime.UnixEpoch,
            MarketCondition = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = StrategyWorkflowContinuationDecision.Proceed,
                Result = new StrategyStageResultEnvelope { ResultType = "Evidence excluded from grid" }
            }
        };
        var row = IntrinsicTimeStrategyUpdatesGridRow.FromView(view);
        var page = new IntrinsicTimeStrategyUpdatesGridPage([row], 1, 50, 1);
        var bytes = MessagePackSerializer.Serialize(page);
        var copy = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyUpdatesGridPage>(bytes);
        copy.Items.Should().ContainSingle().Which.WorkflowId.Should().Be(view.WorkflowId);
        copy.Items[0].WorkflowRevision.Should().Be(7);
        copy.Items[0].Stages[1].Processing.Should().Be("Completed");
        copy.Items[0].Stages[1].Continuation.Should().Be("Proceed");
        MessagePackSerializer.ConvertToJson(bytes).Should().NotContain("Evidence excluded from grid");
    }

    public static TheoryData<Type> QueryTypes => new()
    {
        typeof(GetIntrinsicTimeStrategyWorkflowByIdQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowsByIdsQuery),
        typeof(GetActiveIntrinsicTimeStrategyWorkflowQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowStageStateQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowTimelineQuery),
        typeof(GetRecentIntrinsicTimeStrategyWorkflowsQuery),
        typeof(GetCompletedIntrinsicTimeStrategyWorkflowsQuery),
        typeof(GetStoppedIntrinsicTimeStrategyWorkflowsQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowObservationQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowHistoryPageQuery),
        typeof(GetIntrinsicTimeStrategyUpdatesGridQuery),
    };

    [Theory, MemberData(nameof(QueryTypes))]
    public void Query_keys_match_constructor_and_round_trip(Type queryType)
    {
        var keys = queryType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, keys.Length));
        keys.Take(2).Select(item => item.Property.Name).Should().Equal("Subject", "EntityId");
        var constructor = queryType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var method = typeof(IntrinsicTimeWorkflowQueryWireTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(queryType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
    }
}
