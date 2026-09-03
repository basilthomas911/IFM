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
                PortfolioCommandVerbs.CreatePortfolio,
                PortfolioCommandVerbs.AddPortfolioVersion,
                PortfolioCommandVerbs.ChangePortfolioOperatingState,
                PortfolioCommandVerbs.AddFundToPortfolio,
                PortfolioCommandVerbs.DelegateFundAllocation,
                PortfolioCommandVerbs.DelegateFundRiskEnvelope,
                PortfolioCommandVerbs.RetirePortfolio,
                PortfolioCommandVerbs.DeleteDraftPortfolio,
            ]
        },
        {
            typeof(PortfolioFinancialPolicyCommandActor),
            [
                PortfolioCommandVerbs.CreatePortfolioFinancialPolicy,
                PortfolioCommandVerbs.AddPortfolioFinancialPolicyVersion,
                PortfolioCommandVerbs.ActivateAndAssignPortfolioFinancialPolicy,
                PortfolioCommandVerbs.RetirePortfolioFinancialPolicy,
                PortfolioCommandVerbs.DeleteDraftPortfolioFinancialPolicy,
            ]
        },
        {
            typeof(PortfolioFundCommandActor),
            [
                PortfolioCommandVerbs.CreateFundMandate,
                PortfolioCommandVerbs.AddFundMandateVersion,
                PortfolioCommandVerbs.ChangeFundOperatingState,
                PortfolioCommandVerbs.AssignTradeTemplate,
                PortfolioCommandVerbs.ReserveFundOrderComposition,
                PortfolioCommandVerbs.CreateManualFundOrder,
                PortfolioCommandVerbs.MarkFundOrderComposing,
                PortfolioCommandVerbs.RecordFundOrderComposed,
                PortfolioCommandVerbs.RecordFundOrderRiskOutcome,
                PortfolioCommandVerbs.CancelFundOrderComposition,
                PortfolioCommandVerbs.ExpireFundOrderComposition,
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
            errors.Should().Contain(error => error.ErrorMessage.Contains("Payload", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Cancel_and_expire_use_distinct_exact_command_types()
    {
        typeof(PortfolioCommand<CancelFundOrderCompositionPayload, PortfolioFundId>)
            .Should().NotBe(typeof(PortfolioCommand<ExpireFundOrderCompositionPayload, PortfolioFundId>));
    }

    [Fact]
    public void Cancel_and_expire_payloads_preserve_the_same_wire_shape()
    {
        var orderId = new PortfolioFundOrderId(1, 2, 3);
        var cancel = new CancelFundOrderCompositionPayload(orderId, 4, "reason");
        var expire = new ExpireFundOrderCompositionPayload(orderId, 4, "reason");

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
