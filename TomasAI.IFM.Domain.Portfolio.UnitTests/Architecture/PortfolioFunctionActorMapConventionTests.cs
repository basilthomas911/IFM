using System.Collections;
using System.Reflection;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialAuthorityPreparation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialBookPreparation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.ReferencePreparation.Function.Actor;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Architecture;

public sealed class PortfolioFunctionActorMapConventionTests
{
    [Theory]
    [InlineData(typeof(CapacityReservationFunctionActor))]
    [InlineData(typeof(CapacityConsumptionFunctionActor))]
    [InlineData(typeof(PortfolioOrderCompositionFunctionActor))]
    [InlineData(typeof(PortfolioCloseOrderCompositionFunctionActor))]
    [InlineData(typeof(PortfolioBusinessIdAllocationFunctionActor))]
    [InlineData(typeof(FinancialBookPreparationFunctionActor))]
    [InlineData(typeof(FinancialAuthorityPreparationFunctionActor))]
    [InlineData(typeof(PortfolioReferenceValidationFunctionActor))]
    public void Function_dispatch_declares_frozen_exact_request_and_outcome_maps(Type actor)
    {
        var contract = actor.BaseType!.GetGenericArguments();
        Assert.Single(Map(actor, "_parseMap"));
        foreach (var name in new[] { "_validationMap", "_receiveMap", "_executionPolicyMap" })
            Assert.Equal(contract[1], Assert.IsAssignableFrom<Type>(Assert.Single(Map(actor, name))));
        Assert.True(new[] { contract[^2], contract[^1] }.ToHashSet().SetEquals(Map(actor, "_eventMap").Cast<Type>()));
    }

    static object[] Map(Type actor, string field)
    {
        var map = actor.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Assert.Equal("System.Collections.Frozen", map.GetType().Namespace);
        var contract = map.GetType().GetInterfaces().Single(type => type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        var keys = (IEnumerable)contract.GetProperty("Keys")!.GetValue(map)!;
        return keys.Cast<object>().ToArray();
    }
}
