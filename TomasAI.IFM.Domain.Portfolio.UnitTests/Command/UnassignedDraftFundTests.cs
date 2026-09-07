using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class UnassignedDraftFundTests
{
    static FundMandateReadModel Draft() => PortfolioFundAggregateTests.Draft() with
    { SchemaVersion = 3, PermittedTradeFamilies = [], PermittedTradeStrategyFamilies = [] };

    [Fact]
    public void Unassigned_draft_can_be_created_changed_and_replayed()
    {
        var fund = new PortfolioFundAggregate(); var draft = Draft(); var now = DateTime.UtcNow;
        var created = fund.Create(Guid.NewGuid(), draft, now, "test");
        var changed = fund.AddVersion(Guid.NewGuid(), 1, draft with { Name = "Changed", FundMandateVersion = 2 }, default, now, "test");
        var replay = new PortfolioFundAggregate(); replay.Replay([created, changed]);
        replay.Current!.Name.Should().Be("Changed");
        replay.Current.PermittedTradeStrategyFamilies.Should().BeEmpty();
        replay.Current.Validate().Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unassigned_draft_cannot_activate_even_with_an_activation_context(bool throughVersion)
    {
        var fund = new PortfolioFundAggregate(); var draft = Draft(); var now = DateTime.UtcNow;
        fund.Create(Guid.NewGuid(), draft, now, "test");
        var activation = new FundActivationContext(true, 1, true, true);
        Action activate = () =>
        {
            if (throughVersion) fund.AddVersion(Guid.NewGuid(), 1, draft with { FundMandateVersion = 2, OperatingState = FundOperatingState.Active }, activation, now, "test");
            else fund.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Active, "activate", activation, now, "test");
        };
        activate.Should().Throw<ArgumentException>().WithMessage("*PermittedTradeFamilies*");
        fund.Current!.OperatingState.Should().Be(FundOperatingState.Draft);
    }

    [Fact]
    public void Removing_all_permissions_from_a_draft_is_not_a_legacy_downgrade()
    {
        var fund = new PortfolioFundAggregate(); var draft = Draft(); var now = DateTime.UtcNow;
        var reference = new TradeStrategyFamilyReference(0, 0) { CatalogDeployment = new(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1) };
        fund.Create(Guid.NewGuid(), draft with { PermittedTradeFamilies = ["Configured"], PermittedTradeStrategyFamilies = [reference] }, now, "test");
        fund.AddVersion(Guid.NewGuid(), 1, draft with { FundMandateVersion = 2 }, default, now, "test");
        fund.Current!.SchemaVersion.Should().Be(3);
        fund.Current.PermittedTradeStrategyFamilies.Should().BeEmpty();
    }

    [Theory]
    [InlineData(FundOperatingState.Disabled)]
    [InlineData(FundOperatingState.Retired)]
    public void Unassigned_draft_can_be_disabled_or_retired_without_invalidating_its_history(FundOperatingState state)
    {
        var fund = new PortfolioFundAggregate(); var now = DateTime.UtcNow;
        var created = fund.Create(Guid.NewGuid(), Draft(), now, "test");
        var changed = fund.ChangeState(Guid.NewGuid(), 1, state, "unused", default, now, "test");
        var replay = new PortfolioFundAggregate(); replay.Replay([created, changed]);
        replay.Current!.Validate().Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Older_schemas_still_require_permissions(int schema)
        => (Draft() with { SchemaVersion = schema }).Validate().Should().NotBeEmpty();

    [Fact]
    public void Modern_draft_cannot_replace_exact_permissions_with_legacy_names()
        => (Draft() with { PermittedTradeFamilies = ["Futures"] }).Validate().Should().NotBeEmpty();
}
