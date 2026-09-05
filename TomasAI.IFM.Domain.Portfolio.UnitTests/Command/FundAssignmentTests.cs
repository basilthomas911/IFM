using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class FundAssignmentTests
{
    [Fact]
    public void Typed_mandate_rejects_same_name_wrong_identity_or_version_and_accepts_exact_reference()
    {
        var reference = new TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReference(71, 1);
        var aggregate = CreateFund();
        aggregate.AddVersion(Guid.NewGuid(), 1, aggregate.Current! with { SchemaVersion = 2, FundMandateVersion = 2, PermittedTradeStrategyFamilies = [reference] }, default, Now, "test");
        var assignment = Assignment(Guid.NewGuid(), 1, 1) with { SchemaVersion = 2, FundMandateVersion = 2, TradeStrategyFamily = reference };
        foreach (var wrong in new[] { reference with { TradeStrategyFamilyId = 72 }, reference with { DefinitionVersion = 2 } })
        {
            var action = () => aggregate.AssignTradeTemplate(Guid.NewGuid(), 2, assignment with { TradeStrategyFamily = wrong }, Now, "test");
            action.Should().Throw<ArgumentException>().WithMessage("*trade family*");
        }
        aggregate.AssignTradeTemplate(Guid.NewGuid(), 2, assignment, Now, "test");
        aggregate.Assignments.Single().TradeStrategyFamily.Should().Be(reference);
        var restoredMandate = MessagePack.MessagePackSerializer.Deserialize<FundMandateReadModel>(MessagePack.MessagePackSerializer.Serialize(aggregate.Current!));
        restoredMandate.PermittedTradeStrategyFamilies.Should().Equal(reference);
        var restoredAssignment = MessagePack.MessagePackSerializer.Deserialize<FundTradeTemplateAssignmentReadModel>(MessagePack.MessagePackSerializer.Serialize(assignment));
        restoredAssignment.TradeStrategyFamily.Should().Be(reference);
        var restored = new PortfolioFundAggregate(); restored.RestoreSnapshot(aggregate.CaptureSnapshot());
        restored.Assignments.Single().TradeStrategyFamily.Should().Be(reference);
        var copy = aggregate.Current!.DefensiveCopy(); copy.PermittedTradeStrategyFamilies[0] = reference with { TradeStrategyFamilyId = 99 };
        aggregate.Current.PermittedTradeStrategyFamilies[0].Should().Be(reference);
        var downgrade = () => aggregate.AddVersion(Guid.NewGuid(), 3, aggregate.Current with { FundMandateVersion = 3, SchemaVersion = 1, PermittedTradeStrategyFamilies = [] }, default, Now, "test");
        downgrade.Should().Throw<ArgumentException>().WithMessage("*downgrade*");
    }
    static readonly DateTime Now = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Assignments_are_ordered_and_same_template_windows_cannot_overlap()
    {
        var aggregate = CreateFund();
        var first = Assignment(Guid.NewGuid(), 1, 20);
        var second = Assignment(Guid.NewGuid(), 2, 10);
        aggregate.AssignTradeTemplate(Guid.NewGuid(), 1, first, Now, "admin");
        aggregate.AssignTradeTemplate(Guid.NewGuid(), 2, second, Now, "admin");

        aggregate.EffectiveAssignments(Now.AddDays(1)).Select(x => x.Priority).Should().Equal(10, 20);

        var overlap = Assignment(first.TradeTemplateId, 3, 30);
        var action = () => aggregate.AssignTradeTemplate(Guid.NewGuid(), 3, overlap, Now, "admin");
        action.Should().Throw<InvalidOperationException>().WithMessage("*overlapping*");
    }

    [Fact]
    [Trait("Gate", "PF-05")]
    [Trait("Category", "Portfolio")]
    public void Incompatible_asset_or_horizon_is_rejected()
    {
        var aggregate = CreateFund();
        var action = () => aggregate.AssignTradeTemplate(Guid.NewGuid(), 1,
            Assignment(Guid.NewGuid(), 1, 1) with { AssetType = "FuturesOptions" }, Now, "admin");
        action.Should().Throw<ArgumentException>().WithMessage("*asset type*");
    }

    internal static PortfolioFundAggregate CreateFund()
    {
        var aggregate = new PortfolioFundAggregate();
        aggregate.Create(Guid.NewGuid(), new FundMandateReadModel
        {
            PortfolioId = 101, FundId = 205, FundCode = "DAILY", Name = "Daily",
            FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
            EffectiveFromUtc = Now, DecisionHorizon = "Daily", Objective = "Directional",
            UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"],
            PermittedTradeFamilies = ["DirectionalFuture"], CreatedOnUtc = Now, CreatedBy = "admin",
        }, Now, "admin");
        return aggregate;
    }

    internal static FundTradeTemplateAssignmentReadModel Assignment(Guid templateId, long version, int priority) => new()
    {
        PortfolioId = 101, PortfolioVersion = 1, FundId = 205, FundMandateVersion = 1,
        AssignmentVersion = version, TradeTemplateId = templateId, TradeTemplateVersion = 1,
        Enabled = true, DecisionHorizon = "Daily", UnderlyingUniverse = ["ES"],
        AssetType = "Futures", TradeFamily = "DirectionalFuture", Priority = priority,
        EffectiveFromUtc = Now, TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1,
        OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1,
        CreatedOnUtc = Now, CreatedBy = "admin",
    };
}
