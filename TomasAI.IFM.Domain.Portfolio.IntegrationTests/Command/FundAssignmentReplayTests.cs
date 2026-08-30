using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Command;

public sealed class FundAssignmentReplayTests
{
    [Fact]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Assignment_event_replay_retains_exact_profile_versions()
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var assignment = Assignment(now);
        var history = new PortfolioFundDomainEvent[]
        {
            new FundMandateCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "admin", Mandate(now)),
            new FundTradeTemplateAssigned(Guid.NewGuid(), Guid.NewGuid(), 2, now, "admin", assignment),
        };
        var aggregate = new PortfolioFundAggregate();
        aggregate.Replay(history);
        aggregate.Assignments.Single().Should().BeEquivalentTo(assignment);
    }

    static FundMandateReadModel Mandate(DateTime now) => new() { PortfolioId=101, FundId=205, FundCode="DAILY", Name="Daily", FundMandateVersion=1, TradingYear=2026, OperatingState=FundOperatingState.Draft, EffectiveFromUtc=now, DecisionHorizon="Daily", Objective="Directional", UnderlyingUniverse=["ES"], EligibleAssetTypes=["Futures"], PermittedTradeFamilies=["DirectionalFuture"], CreatedOnUtc=now, CreatedBy="admin" };
    static FundTradeTemplateAssignmentReadModel Assignment(DateTime now) => new() { PortfolioId=101, PortfolioVersion=1, FundId=205, FundMandateVersion=1, AssignmentVersion=1, TradeTemplateId=Guid.NewGuid(), TradeTemplateVersion=3, Enabled=true, DecisionHorizon="Daily", UnderlyingUniverse=["ES"], AssetType="Futures", TradeFamily="DirectionalFuture", Priority=1, EffectiveFromUtc=now, TradeSelectionHintProfileId=Guid.NewGuid(), TradeSelectionHintProfileVersion=2, OrderCompositionProfileId=Guid.NewGuid(), OrderCompositionProfileVersion=4, CreatedOnUtc=now, CreatedBy="admin" };
}
