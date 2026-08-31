using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class PortfolioAllocationRiskTests
{
    static readonly DateTime Now = new(2026,8,29,14,0,0,DateTimeKind.Utc);

    [Fact]
    [Trait("Gate","PF-06")]
    [Trait("Category","Portfolio")]
    public void Envelope_cannot_exceed_the_authoritative_allocation()
    {
        var aggregate = Portfolio();
        aggregate.DelegateAllocation(Guid.NewGuid(), 2, Allocation(), Now, "risk-admin");
        var action = () => aggregate.DelegateRiskEnvelope(Guid.NewGuid(), 3, Envelope() with { AllocatedCapital = 110_000m, AvailableCapital = 100_000m }, Now, "risk-admin");
        action.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    [Theory]
    [InlineData(FundCapacityState.Available,true)]
    [InlineData(FundCapacityState.Constrained,true)]
    [InlineData(FundCapacityState.Blocked,false)]
    [InlineData(FundCapacityState.ReduceOnly,false)]
    [Trait("Gate","PF-06")]
    [Trait("Category","Portfolio")]
    public void Capacity_state_controls_new_exposure(FundCapacityState state, bool expected) =>
        (Envelope() with { CapacityState = state }).PermitsNewExposureAt(Now.AddDays(1)).Should().Be(expected);

    internal static PortfolioAggregate Portfolio()
    {
        var p = new PortfolioAggregate();
        p.Create(Guid.NewGuid(), new PortfolioReadModel { PortfolioId=101, Name="Core", PortfolioVersion=1, OperatingState=PortfolioOperatingState.Draft, EffectiveFromUtc=Now, CreatedOnUtc=Now, CreatedBy="admin" }, Now, "admin");
        p.AddFund(Guid.NewGuid(),1,new PortfolioFundId(101,205),Now,"admin");
        return p;
    }
    internal static FundAllocationReadModel Allocation() => new() { PortfolioId=101, PortfolioVersion=1, FundId=205, FundMandateVersion=1, AllocationVersion=1, TargetWeight=.5m, MinimumWeight=.25m, MaximumWeight=.75m, AllocatedCapital=100_000m, Currency="USD", EffectiveFromUtc=Now, SourcePolicyId=9001, SourcePolicyVersion=1, CreatedOnUtc=Now, CreatedBy="risk-admin" };
    internal static FundRiskEnvelopeReadModel Envelope() => new() { PortfolioId=101, PortfolioVersion=1, FundId=205, FundMandateVersion=1, EnvelopeId=Guid.Parse("22222222-2222-2222-2222-222222222222"), EnvelopeVersion=1, CapacityState=FundCapacityState.Available, Currency="USD", AllocatedCapital=100_000m, AvailableCapital=80_000m, MaximumRiskPerTrade=1_000m, MaximumAggregateRisk=5_000m, MaximumMargin=50_000m, MaximumGrossNotional=500_000m, MaximumContracts=10, MaximumOpenPositions=5, RemainingLossBudget=10_000m, EffectiveFromUtc=Now, ExpiresAtUtc=Now.AddDays(30), SourcePolicyId=9001, SourcePolicyVersion=1, CreatedOnUtc=Now, CreatedBy="risk-admin" };
}
