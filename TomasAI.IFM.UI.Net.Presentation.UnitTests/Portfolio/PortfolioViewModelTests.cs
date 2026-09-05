using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Portfolio;

public sealed class PortfolioViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_portfolio_selection_cannot_replace_the_current_funds_or_revision(bool delayRevision)
    {
        var queries = Substitute.For<IPortfolioQueryApi>();
        var first = ValidPortfolio();
        var second = first with { PortfolioId = 102 };
        var oldFunds = new TaskCompletionSource<ServiceResult<PortfolioPage<FundMandateReadModel>>>();
        var oldRevision = new TaskCompletionSource<ServiceResult<PortfolioAggregateRevision>>();
        var firstFund = new FundMandateReadModel { PortfolioId = 101, FundId = 201 };
        var secondFund = new FundMandateReadModel { PortfolioId = 102, FundId = 202 };
        queries.GetFundsAsync(101, null, 100, null, Arg.Any<CancellationToken>()).Returns(oldFunds.Task);
        queries.GetPortfolioRevisionAsync(101, Arg.Any<CancellationToken>()).Returns(oldRevision.Task);
        queries.GetFundsAsync(102, null, 100, null, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = [secondFund] }));
        queries.GetPortfolioRevisionAsync(102, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = 102, Revision = 8 }));
        if (delayRevision)
            oldFunds.SetResult(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = [firstFund] }));
        var vm = new PortfolioAdministrationViewModel(queries, Substitute.For<IPortfolioCommandApi>(),
            Substitute.For<IPortfolioFundCommandApi>(), Substitute.For<IPortfolioIdentityApi>(), true);
        var stale = vm.SelectPortfolioAsync(first);
        await vm.SelectPortfolioAsync(second);
        oldFunds.TrySetResult(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = [firstFund] }));
        oldRevision.SetResult(new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = 101, Revision = 3 }));
        await stale;
        vm.SelectedPortfolio.Should().Be(second);
        vm.Funds.Should().Equal(secondFund);
        vm.PortfolioRevision.Should().Be(8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_fund_configuration_cannot_overwrite_a_new_fund_or_portfolio(bool switchPortfolio)
    {
        var queries = Substitute.For<IPortfolioQueryApi>();
        var portfolio = ValidPortfolio();
        var first = new FundMandateReadModel { PortfolioId = 101, FundId = 201, FundMandateVersion = 1 };
        var second = first with { FundId = 202 };
        queries.GetFundsAsync(Arg.Any<int>(), null, 100, null, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = [first, second] }));
        queries.GetPortfolioRevisionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = 101, Revision = 7 }));
        queries.GetFundRevisionAsync(101, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = 101, Revision = 5 }));
        var pending = new TaskCompletionSource<ServiceResult<FundAllocationReadModel>>();
        queries.GetFundAllocationAsync(101, 201, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var allocation = new FundAllocationReadModel { PortfolioId = 101, FundId = 202 };
        queries.GetFundAllocationAsync(101, 202, Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundAllocationReadModel>(allocation));
        queries.GetFundRiskEnvelopeAsync(101, 202, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(
            new ServiceFailed<FundRiskEnvelopeReadModel>(34001, "none"));
        queries.GetAssignmentsAsync(101, 202, 1, Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundTradeTemplateAssignmentReadModel[]>([]));
        var vm = new PortfolioAdministrationViewModel(queries, Substitute.For<IPortfolioCommandApi>(),
            Substitute.For<IPortfolioFundCommandApi>(), Substitute.For<IPortfolioIdentityApi>(), true);
        await vm.SelectPortfolioAsync(portfolio);
        var stale = vm.SelectFundAsync(first);
        if (switchPortfolio) await vm.SelectPortfolioAsync(portfolio with { PortfolioId = 102 });
        else await vm.SelectFundAsync(second);
        pending.SetResult(new ServiceOk<FundAllocationReadModel>(new() { PortfolioId = 101, FundId = 201 }));
        await stale;
        if (switchPortfolio)
        {
            vm.SelectedFund.Should().BeNull(); vm.Allocation.Should().BeNull(); vm.FundRevision.Should().Be(0);
        }
        else
        {
            vm.SelectedFund.Should().Be(second); vm.Allocation.Should().Be(allocation);
        }
        await queries.DidNotReceive().GetFundRiskEnvelopeAsync(101, 201, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public async Task Reader_cannot_mutate_and_administrator_enters_pending_projection_after_commit()
    {
        var queries = Substitute.For<IPortfolioQueryApi>();
        var commands = Substitute.For<IPortfolioCommandApi>();
        var fundCommands = Substitute.For<IPortfolioFundCommandApi>();
        var identities = Substitute.For<IPortfolioIdentityApi>();
        var model = ValidPortfolio();
        commands.CreatePortfolioAsync(model, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var reader = new PortfolioAdministrationViewModel(queries, commands, fundCommands, identities, false);
        var admin = new PortfolioAdministrationViewModel(queries, commands, fundCommands, identities, true);

        await reader.CreatePortfolioAsync(model);
        await admin.CreatePortfolioAsync(model);

        reader.State.Should().Be(PortfolioUiState.Unauthorized);
        admin.State.Should().Be(PortfolioUiState.PendingProjection);
        await commands.Received(1).CreatePortfolioAsync(model, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public async Task Create_path_consumes_typed_allocated_identity_and_never_fabricates_failure()
    {
        var identities = Substitute.For<IPortfolioIdentityApi>();
        identities.AllocatePortfolioIdAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioBusinessIdAllocation>(new() { Kind = PortfolioBusinessIdentityKind.Portfolio, Value = 7001, CorrelationId = Guid.NewGuid() }),
            new ServiceFailed<PortfolioBusinessIdAllocation>(34012, "sequence unavailable"));
        var vm = new PortfolioAdministrationViewModel(
            Substitute.For<IPortfolioQueryApi>(), Substitute.For<IPortfolioCommandApi>(),
            Substitute.For<IPortfolioFundCommandApi>(), identities, true);

        var allocated = await vm.AllocatePortfolioIdAsync();
        var failed = await vm.AllocatePortfolioIdAsync();

        allocated.DisplayId.Should().Be("7001");
        failed.IsSuccessful.Should().BeFalse();
        failed.DisplayId.Should().BeEmpty();
        vm.State.Should().Be(PortfolioUiState.ValidationError);
    }

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public async Task Delete_path_accepts_only_selected_Draft_and_sends_its_current_aggregate_revision()
    {
        var queries = Substitute.For<IPortfolioQueryApi>();
        var commands = Substitute.For<IPortfolioCommandApi>();
        var draft = ValidPortfolio() with { OperatingState = PortfolioOperatingState.Draft, ActivePolicyId = 0, ActivePolicyVersion = 0 };
        queries.GetFundsAsync(draft.PortfolioId, null, 100, null, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = [] }));
        queries.GetPortfolioRevisionAsync(draft.PortfolioId, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = draft.PortfolioId, Revision = 9, SourceEventId = 99 }));
        commands.DeleteDraftPortfolioAsync(new PortfolioId(draft.PortfolioId), 9, "duplicate", Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var vm = new PortfolioAdministrationViewModel(queries, commands, Substitute.For<IPortfolioFundCommandApi>(), Substitute.For<IPortfolioIdentityApi>(), true);
        await vm.SelectPortfolioAsync(draft);

        var deleted = await vm.DeleteDraftPortfolioAsync("duplicate");

        deleted.Should().BeTrue();
        vm.SelectedPortfolio.Should().BeNull();
        vm.PortfolioRevision.Should().Be(0);
        vm.Message.Should().Contain("ID remains consumed");
        await commands.Received(1).DeleteDraftPortfolioAsync(new PortfolioId(draft.PortfolioId), 9, "duplicate", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public async Task Mutations_use_projection_aggregate_revisions_instead_of_business_versions()
    {
        var queries = Substitute.For<IPortfolioQueryApi>();
        var commands = Substitute.For<IPortfolioCommandApi>();
        var fundCommands = Substitute.For<IPortfolioFundCommandApi>();
        var portfolio = ValidPortfolio();
        var fund = new FundMandateReadModel { PortfolioId = 101, FundId = 201, FundMandateVersion = 1 };
        queries.GetFundsAsync(101, null, 100, null, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = [fund] }));
        queries.GetPortfolioRevisionAsync(101, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = 101, Revision = 7, SourceEventId = 70 }));
        queries.GetFundRevisionAsync(101, 201, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioAggregateRevision>(new() { PortfolioId = 101, FundId = 201, Revision = 5, SourceEventId = 71 }));
        queries.GetFundAllocationAsync(101, 201, Arg.Any<CancellationToken>()).Returns(new ServiceFailed<FundAllocationReadModel>(34001, "none"));
        queries.GetFundRiskEnvelopeAsync(101, 201, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ServiceFailed<FundRiskEnvelopeReadModel>(34001, "none"));
        queries.GetAssignmentsAsync(101, 201, 1, Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundTradeTemplateAssignmentReadModel[]>([]));
        commands.ChangePortfolioStateAsync(new PortfolioId(101), 7, PortfolioOperatingState.Paused, "review", Arg.Any<CancellationToken>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        fundCommands.ChangeFundStateAsync(new(101, 201), 5, FundOperatingState.Paused, "review", Arg.Any<CancellationToken>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var vm = new PortfolioAdministrationViewModel(queries, commands, fundCommands, Substitute.For<IPortfolioIdentityApi>(), true);

        await vm.SelectPortfolioAsync(portfolio);
        await vm.ChangePortfolioStateAsync(PortfolioOperatingState.Paused, "review");
        await vm.SelectFundAsync(fund);
        await vm.ChangeFundStateAsync(FundOperatingState.Paused, "review");

        await commands.Received(1).ChangePortfolioStateAsync(new PortfolioId(101), 7, PortfolioOperatingState.Paused, "review", Arg.Any<CancellationToken>());
        await fundCommands.Received(1).ChangeFundStateAsync(new(101, 201), 5, FundOperatingState.Paused, "review", Arg.Any<CancellationToken>());
        vm.FundRevision.Should().Be(6);
    }

    static PortfolioReadModel ValidPortfolio() => new()
    {
        PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
        OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc),
        CreatedOnUtc = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "admin",
    };
}
