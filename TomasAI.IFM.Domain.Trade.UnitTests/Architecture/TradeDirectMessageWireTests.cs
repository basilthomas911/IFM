using System.Reflection;
using FluentAssertions;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Architecture;

/// <summary>Guards published direct-key Trade messages while incompatible layouts migrate by version.</summary>
public sealed class TradeDirectMessageWireTests
{
    public static TheoryData<Type> MessageTypes => new()
    {
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Futures.GetFuturesTradeQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Futures.Position.GetFuturesTradePositionQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Futures.Position.GetFuturesTradePositionHistoryQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Futures.Option.GetIronCondorOptionTradeQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Futures.Option.GetVerticalSpreadOptionTradeQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Futures.Option.GetIronCondorOptionTradesQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Futures.Option.GetVerticalSpreadOptionTradesQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.GetBrokerOrderQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.GetBrokerOrdersForTradeOrderQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.GetTradeOrderQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Execution.GetOrderExecutionQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.CreateBrokerOrderCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.RecordBrokerDispatchCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.RecordBrokerOrderObservationCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.RequestBrokerOrderLimitUpdateCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.RequestBrokerOrderCancelCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Broker.BrokerOrderObservationReceivedEvent")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.Execution.StartOrderExecutionCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.CreateTradeOrderCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Order.AmendTradeOrderCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.ExitPositionWorkflowStartedEvent")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.StartIronCondorExitPositionWorkflowCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.StartVerticalSpreadExitPositionWorkflowCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.StartFuturesExitPositionWorkflowCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Trade.Position.OpenPositionRoutesChangedEvent")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Trade.Position.ChangeTradeLegDataCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.GetMarketConditionAssessmentReferenceQuery")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteMarketConditionAssessmentCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteOrderCompositionPipelineCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteRiskManagementPipelineCommand")!,
        System.Reflection.Assembly.Load("TomasAI.IFM.Domain.Trade.Shared").GetType("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteTradeSelectionPipelineCommand")!,
    };

    [Theory, MemberData(nameof(MessageTypes))]
    public void Numeric_keys_match_serialization_constructor_and_round_trip(Type messageType)
    {
        messageType.GetCustomAttribute<MessagePackObjectAttribute>()!.AllowPrivate.Should().BeTrue();
        var keys = messageType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, keys.Length));
        var constructor = messageType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var method = typeof(TradeDirectMessageWireTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(messageType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
    }
}
