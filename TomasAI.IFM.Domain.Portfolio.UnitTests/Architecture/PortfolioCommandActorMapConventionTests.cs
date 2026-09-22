using System.Collections;
using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Architecture;

public sealed class PortfolioCommandActorMapConventionTests
{
    public static TheoryData<Type, string[]> Actors => new()
    {
        {
            typeof(PortfolioCommandActor),
            [
                CreatePortfolioCommand.Verb,
                AddPortfolioVersionCommand.Verb,
                ChangePortfolioOperatingStateCommand.Verb,
                AddFundToPortfolioCommand.Verb,
                DelegateFundAllocationCommand.Verb,
                DelegateFundRiskEnvelopeCommand.Verb,
                RetirePortfolioCommand.Verb,
                DeleteDraftPortfolioCommand.Verb,
            ]
        },
        {
            typeof(PortfolioFinancialPolicyCommandActor),
            [
                CreatePortfolioFinancialPolicyCommand.Verb,
                AddPortfolioFinancialPolicyVersionCommand.Verb,
                ActivateAndAssignPortfolioFinancialPolicyCommand.Verb,
                RetirePortfolioFinancialPolicyCommand.Verb,
                DeleteDraftPortfolioFinancialPolicyCommand.Verb,
            ]
        },
        {
            typeof(PortfolioFundCommandActor),
            [
                CreateFundMandateCommand.Verb,
                AddFundMandateVersionCommand.Verb,
                ChangeFundOperatingStateCommand.Verb,
                AssignTradeTemplateCommand.Verb,
                ReserveFundOrderCompositionCommand.Verb,
                CreateManualFundOrderCommand.Verb,
                AddManualFundOrderTradeCommand.Verb,
                RemoveManualFundOrderTradeCommand.Verb,
                ChangeManualFundOrderTradeStateCommand.Verb,
                CloseManualFundOrderCommand.Verb,
                DeleteManualFundOrderCommand.Verb,
                MarkFundOrderComposingCommand.Verb,
                RecordFundOrderComposedCommand.Verb,
                RecordFundOrderRiskOutcomeCommand.Verb,
                AuthorizeFundOrderRiskCommand.Verb,
                SynchronizeFundRiskOutcomeCommand.Verb,
                CancelFundOrderCompositionCommand.Verb,
                ExpireFundOrderCompositionCommand.Verb,
            ]
        },
    };

    [Theory]
    [MemberData(nameof(Actors))]
    public void Parse_validation_and_receive_maps_expose_the_same_complete_command_set(
        Type actorType,
        string[] expectedVerbs)
    {
        var parseMap = GetMap(actorType, "_parseMap");
        var validationMap = GetMap(actorType, "_validationMap");
        var receiveMap = GetMap(actorType, "_receiveMap");

        parseMap.Keys.Cast<string>().Should().BeEquivalentTo(expectedVerbs);
        validationMap.Count.Should().Be(parseMap.Count);
        receiveMap.Keys.Cast<Type>().Should().BeEquivalentTo(validationMap.Keys.Cast<Type>());
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void Every_mapped_validator_accumulates_default_command_errors_without_throwing(
        Type actorType,
        string[] _)
    {
        var validationMap = GetMap(actorType, "_validationMap");

        foreach (DictionaryEntry entry in validationMap)
        {
            var command = Activator.CreateInstance((Type)entry.Key)!;
            var errors = ((Delegate)entry.Value).DynamicInvoke(command).Should().BeAssignableTo<List<ValidationError>>().Subject;

            errors.Should().Contain(error => error.ErrorMessage.Contains("CommandId", StringComparison.Ordinal));
            errors.Should().Contain(error => error.ErrorMessage.Contains("EntityId", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Cancel_and_expire_use_distinct_exact_command_types()
    {
        typeof(CancelFundOrderCompositionCommand)
            .Should().NotBe(typeof(ExpireFundOrderCompositionCommand));
    }

    [Fact]
    public void Cancel_and_expire_payloads_preserve_the_same_wire_shape()
    {
        var orderId = new PortfolioFundOrderId(1, 2, 3);
        var cancel = new CancelFundOrderCompositionCommand(orderId, 4, "reason");
        var expire = new ExpireFundOrderCompositionCommand(orderId, 4, "reason");

        MessagePackSerializer.Serialize(cancel)
            .Should().Equal(MessagePackSerializer.Serialize(expire));
    }

    static IDictionary GetMap(Type actorType, string fieldName)
    {
        var field = actorType.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        field.Should().NotBeNull($"{actorType.Name} must declare {fieldName}");
        return field!.GetValue(null).Should().BeAssignableTo<IDictionary>().Subject;
    }
}
