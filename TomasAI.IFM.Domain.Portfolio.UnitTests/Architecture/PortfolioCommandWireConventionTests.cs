using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Architecture;

/// <summary>Guards the published numeric wire layouts of compatible Portfolio command families.</summary>
public sealed class PortfolioCommandWireConventionTests
{
    public static TheoryData<Type> CommandTypes => new()
    {
        typeof(RetirePortfolioCommand),
        typeof(AddFundToPortfolioCommand),
        typeof(DeleteDraftPortfolioCommand),
        typeof(DelegateFundRiskEnvelopeCommand),
        typeof(DelegateFundAllocationCommand),
        typeof(CreatePortfolioCommand),
        typeof(ChangePortfolioOperatingStateCommand),
        typeof(AddPortfolioVersionCommand),
        typeof(SynchronizeFundRiskOutcomeCommand),
        typeof(ReserveFundOrderCompositionCommand),
        typeof(RemoveManualFundOrderTradeCommand),
        typeof(RecordFundOrderRiskOutcomeCommand),
        typeof(RecordFundOrderComposedCommand),
        typeof(MarkFundOrderComposingCommand),
        typeof(ExpireFundOrderCompositionCommand),
        typeof(DeleteManualFundOrderCommand),
        typeof(CreateManualFundOrderCommand),
        typeof(CreateFundMandateCommand),
        typeof(CloseManualFundOrderCommand),
        typeof(ChangeManualFundOrderTradeStateCommand),
        typeof(ChangeFundOperatingStateCommand),
        typeof(CancelFundOrderCompositionCommand),
        typeof(AuthorizeFundOrderRiskCommand),
        typeof(AssignTradeTemplateCommand),
        typeof(AddManualFundOrderTradeCommand),
        typeof(AddFundMandateVersionCommand),
        typeof(RetirePortfolioFinancialPolicyCommand),
        typeof(DeleteDraftPortfolioFinancialPolicyCommand),
        typeof(CreatePortfolioFinancialPolicyCommand),
        typeof(AddPortfolioFinancialPolicyVersionCommand),
        typeof(ActivateAndAssignPortfolioFinancialPolicyCommand),
    };

    [Theory, MemberData(nameof(CommandTypes))]
    public void Numeric_keys_match_serialization_constructor_and_round_trip(Type messageType)
    {
        messageType.GetCustomAttribute<MessagePackObjectAttribute>()!.AllowPrivate.Should().BeTrue();
        var keys = messageType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, keys.Length));
        keys.Take(6).Select(item => item.Property.Name)
            .Should().Equal("CommandId", "Subject", "PostEvents", "EntityId", "ErrorCode", "RouteTo");
        var constructor = messageType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var method = typeof(PortfolioCommandWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(messageType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(copy)).Should().NotBeNull();
    }
}
