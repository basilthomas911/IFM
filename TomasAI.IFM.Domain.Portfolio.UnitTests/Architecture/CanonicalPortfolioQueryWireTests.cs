using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Architecture;

/// <summary>Guards the single direct-key contract and owning route for each Portfolio query.</summary>
public sealed class CanonicalPortfolioQueryWireTests
{
    public static TheoryData<Type, int, string> Contracts => new()
    {
        { typeof(GetPortfolioQuery), 7, PortfolioQueryRoutes.Portfolio },
        { typeof(GetPortfolioRevisionQuery), 6, PortfolioQueryRoutes.Portfolio },
        { typeof(GetPortfoliosQuery), 8, PortfolioQueryRoutes.Portfolio },
        { typeof(AllocatePortfolioBusinessIdQuery), 6, PortfolioQueryRoutes.Portfolio },
        { typeof(GetFundQuery), 8, PortfolioQueryRoutes.Fund },
        { typeof(GetFundRevisionQuery), 7, PortfolioQueryRoutes.Fund },
        { typeof(GetFundsQuery), 9, PortfolioQueryRoutes.Fund },
        { typeof(GetFundAllocationQuery), 7, PortfolioQueryRoutes.Fund },
        { typeof(GetFundRiskEnvelopeQuery), 8, PortfolioQueryRoutes.Fund },
        { typeof(GetFundTemplateAssignmentsQuery), 8, PortfolioQueryRoutes.Fund },
        { typeof(ResolveForSelectionQuery), 14, PortfolioQueryRoutes.Fund },
        { typeof(GetPortfolioFundStrategySnapshotQuery), 14, PortfolioQueryRoutes.Fund },
        { typeof(GetFundOrderByOrderIdQuery), 6, PortfolioQueryRoutes.Fund },
        { typeof(GetFundOrderTradeByTradeIdQuery), 6, PortfolioQueryRoutes.Fund },
        { typeof(GetFundCompositionByWorkflowQuery), 6, PortfolioQueryRoutes.Fund },
        { typeof(GetFundOrdersPageQuery), 10, PortfolioQueryRoutes.Fund },
        { typeof(GetFundOrderTradesPageQuery), 8, PortfolioQueryRoutes.Fund },
        { typeof(GetPortfolioFundStrategyReferenceCombinationsQuery), 7, PortfolioQueryRoutes.Fund },
        { typeof(GetPortfolioFinancialPolicyQuery), 7, PortfolioQueryRoutes.FinancialPolicy },
        { typeof(GetPortfolioFinancialPoliciesQuery), 7, PortfolioQueryRoutes.FinancialPolicy },
        { typeof(GetActivePortfolioFinancialPolicyQuery), 6, PortfolioQueryRoutes.FinancialPolicy },
    };

    [Theory, MemberData(nameof(Contracts))]
    public void Query_has_contiguous_keys_direct_entity_id_and_one_canonical_route(Type type, int keyCount, string actor)
    {
        var keys = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(entry => entry.Key.HasValue)
            .OrderBy(entry => entry.Key)
            .ToArray();
        keys.Select(entry => entry.Key!.Value).Should().Equal(Enumerable.Range(0, keyCount));
        keys[1].Property.Name.Should().Be("EntityId");
        type.GetField("Actor")!.GetRawConstantValue().Should().Be(actor);
        type.GetField("Verb")!.GetRawConstantValue().Should().Be(type.Name[..^"Query".Length]);
        var method = typeof(CanonicalPortfolioQueryWireTests)
            .GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(type).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var encoded = MessagePackSerializer.Serialize(new T());
        var restored = MessagePackSerializer.Deserialize<T>(encoded);
        restored.Should().NotBeNull();
        MessagePackSerializer.Serialize(restored).Should().Equal(encoded);
    }
}
