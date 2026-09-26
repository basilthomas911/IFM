using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Architecture;

/// <summary>Guards the published Trade pipeline query wire layouts.</summary>
public sealed class TradePipelineQueryWireTests
{
    public static TheoryData<Type> QueryTypes => new()
    {
        typeof(GetTradeSelectionInvocationQuery),
        typeof(GetTradeSelectionResultQuery),
        typeof(GetTradeSelectionHistoryPageQuery),
        typeof(GetRiskInvocationQuery),
        typeof(GetRiskResultQuery),
        typeof(GetRiskHistoryPageQuery),
        typeof(GetRegimeDiscoveryQuery),
        typeof(GetRegimeDiscoveryDecisionReferenceQuery),
        typeof(GetOrderCompositionInvocationQuery),
        typeof(GetOrderCompositionResultQuery),
        typeof(GetOrderCompositionHistoryPageQuery),
        typeof(GetMarketConditionQuery),
        typeof(GetLatestMarketConditionQuery),
        typeof(GetMarketConditionHistoryQuery),
        typeof(GetMarketConditionDecisionReferenceQuery),
        typeof(GetMarketConditionAssessmentQuery),
        typeof(GetMarketConditionAssessmentHistoryQuery),
    };

    [Theory, MemberData(nameof(QueryTypes))]
    public void Query_keys_match_constructor_and_round_trip(Type queryType)
    {
        queryType.GetCustomAttribute<MessagePackObjectAttribute>()!.AllowPrivate.Should().BeTrue();
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
        var method = typeof(TradePipelineQueryWireTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(queryType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        MessagePackSerializer.Deserialize<T>(bytes).Should().NotBeNull();
    }
}
