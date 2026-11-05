using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"), Trait("Category", "PortfolioFinancial"), Trait("Gate", "PF-FIN-04")]
public sealed class PortfolioAuthorityFenceTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Reference_receipt_is_durable_and_cannot_authorize_a_different_mandate()
    {
        var request = ValidatePortfolioReferenceCommand.ForOwnerCommand(Guid.NewGuid()) with
        {
            Access = PortfolioAccessContext.Administrator("reference-receipt-integration"),
            Mandate = new() { PortfolioId = 101, FundId = 202 }
        };
        var events = new PortfolioEventStore(fixture.EventSourceDb);
        await FluentActions.Awaiting(() => events.VerifyReferencePreparationAsync(request))
            .Should().ThrowAsync<InvalidOperationException>();
        var receipt = new PortfolioReferenceValidationCompletedEvent
        {
            Id = request.CommandId, CommandId = request.CommandId, Subject = request.Subject, EntityId = request.EntityId,
            ReceivedOn = DateTime.UtcNow, EventSource = request.EventSource, InputHash = request.ComputeInputHash()
        };
        await fixture.EventSourceDb.SaveEventsAsync(request.StreamId, request.CommandId, [receipt], 0, CancellationToken.None);
        await events.VerifyReferencePreparationAsync(request);
        await FluentActions.Awaiting(() => events.VerifyReferencePreparationAsync(request with
            { Mandate = request.Mandate! with { FundId = 203 } }))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => events.VerifyReferencePreparationAsync(request with
            { OwnerCommandId = Guid.NewGuid() }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Fund_write_requires_prepared_identifier_to_match_the_exact_committed_receipt()
    {
        var book = await CapacityReservationIntegrationTests.FundedBook();
        var fund = new PortfolioFundId(book.PortfolioId, book.Funds[0].FundId);
        var access = PortfolioAccessContext.Workflow("prepared-identity-integration");
        var changed = new FundCompositionStateChangedEvent(Guid.NewGuid(), Guid.NewGuid(), 2, DateTime.UtcNow, "integration",
            new() { PortfolioId = fund.PortfolioId, FundId = fund.FundId, OrderId = 701, Status = "Composed" });
        var evidence = new PortfolioPreparedIdentifiers(fund, changed.CommandId, access, 701, []);
        var fence = new PortfolioAuthorityFence(Transactions());
        var append = new PortfolioAuthorityAppend(fund.FundId, $"PortfolioFund.{fund.Format()}", changed, 1, false)
        {
            PreparedIdentifiers = evidence
        };
        await FluentActions.Awaiting(() => fence.AppendAsync(fund.PortfolioId, append))
            .Should().ThrowAsync<FinancialOperationException>();

        var operation = FundOrderPreparation.AllocationOperationId(fund, changed.CommandId, PortfolioBusinessIdentityKind.Order, 0);
        var request = AllocatePortfolioBusinessIdCommand.ForOperation(operation) with { Kind = PortfolioBusinessIdentityKind.Order, Access = access };
        var receipt = new PortfolioBusinessIdAllocationCompletedEvent
        {
            Id = operation, CommandId = operation, Subject = request.Subject, EntityId = request.EntityId,
            ReceivedOn = DateTime.UtcNow, EventSource = request.EventSource,
            InputHash = FinancialCanonicalHash.Compute(new { request.Kind, request.Access }),
            Value = new() { Kind = request.Kind, Value = 701, CorrelationId = operation, OperationId = operation }
        };
        await fixture.EventSourceDb.SaveEventsAsync(request.StreamId, request.CommandId, [receipt], 0, CancellationToken.None);

        await FluentActions.Awaiting(() => fence.AppendAsync(fund.PortfolioId, append with
            { PreparedIdentifiers = evidence with { OrderId = 702 } }))
            .Should().ThrowAsync<FinancialOperationException>();
        await fence.AppendAsync(fund.PortfolioId, append);
        var refreshed = await new PortfolioFinancialStore(Transactions()).ReadBookAsync(book.PortfolioId);
        refreshed!.Funds[0].FundStreamVersion.Should().Be(2);
    }

    [Fact]
    public async Task Retiring_a_referenced_policy_is_rejected_under_the_Portfolio_fence()
    {
        var book = await CapacityReservationIntegrationTests.FundedBook();
        var policyId = book.Funds[0].Reference.PolicyId;
        var events = new PortfolioEventStore(fixture.EventSourceDb, new PortfolioAuthorityFence(Transactions()));
        var now = DateTime.UtcNow;
        var current = new PortfolioOperatingStateChangedEvent(Guid.NewGuid(), Guid.NewGuid(), 2, now, "integration",
            PortfolioOperatingState.Active, "qualified") with
        {
            ProjectionFacts = new()
            {
                Revision = 2, UpdatedAtUtc = now,
                Portfolio = new()
                {
                    PortfolioId = book.PortfolioId, PortfolioVersion = 1, OperatingState = PortfolioOperatingState.Active,
                    ActivePolicyId = policyId, ActivePolicyVersion = 1
                }
            }
        };
        await events.AppendPortfolioAsync(new(book.PortfolioId), current, 1);
        var retirement = new PortfolioFinancialPolicyRetiredEvent(Guid.NewGuid(), Guid.NewGuid(), 2, now, "integration", 1, "test");

        var rejected = await FluentActions.Awaiting(() => events.AppendPolicyAsync(new(book.PortfolioId, policyId), retirement, 1))
            .Should().ThrowAsync<FinancialOperationException>();

        rejected.Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
    }

    [Fact]
    public async Task Real_portfolio_event_writer_invalidates_financial_authority_in_the_same_transaction()
    {
        var book = await CapacityReservationIntegrationTests.FundedBook();
        var events = new PortfolioEventStore(fixture.EventSourceDb, new PortfolioAuthorityFence(Transactions()));
        var changed = new PortfolioOperatingStateChangedEvent(Guid.NewGuid(), Guid.NewGuid(), 2, DateTime.UtcNow, "integration", PortfolioOperatingState.Disabled, "test revocation");
        await events.AppendPortfolioAsync(new(book.PortfolioId), changed, 1);
        var snapshot = await new FinancialQueryStore(Transactions()).ReadAsync(new FinancialReadScope { PortfolioId = book.PortfolioId, Access = new("integration", ["PortfolioAdministrator"]) }, new GetAccountBalancesRequest());
        snapshot.FinancialRevision.Should().Be(2); snapshot.Value!.OperatingState.Should().Be("NeedsRefresh");
        var request = await CapacityReservationIntegrationTests.ReserveRequest(book, 700);
        request = request with { ExpectedFinancialRevision = 2 };
        var error = await FluentActions.Awaiting(() => CapacityReservationIntegrationTests.Reserve(request)).Should().ThrowAsync<FinancialOperationException>();
        error.Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
    }

    [Fact]
    public async Task Order_status_event_advances_fund_source_fence_without_revoking_unchanged_mandate()
    {
        var book = await CapacityReservationIntegrationTests.FundedBook();
        var events = new PortfolioEventStore(fixture.EventSourceDb, new PortfolioAuthorityFence(Transactions()));
        var changed = new FundCompositionStateChangedEvent(Guid.NewGuid(), Guid.NewGuid(), 2, DateTime.UtcNow, "integration",
            new() { PortfolioId = book.PortfolioId, FundId = book.Funds[0].FundId, OrderId = 123, Status = "Composed" });
        await events.AppendFundAsync(new PortfolioFundId(book.PortfolioId, book.Funds[0].FundId), changed, 1);
        var refreshed = await new PortfolioFinancialStore(Transactions()).ReadBookAsync(book.PortfolioId);
        refreshed!.Funds[0].FundStreamVersion.Should().Be(2); refreshed.Funds[0].Reference.Should().Be(book.Funds[0].Reference);
        var request = await CapacityReservationIntegrationTests.ReserveRequest(refreshed, 700);
        (await CapacityReservationIntegrationTests.Reserve(request)).Receipt.FinancialRevision.Should().Be(2);
    }

    [Fact]
    public async Task Failed_configuration_compare_and_swap_cannot_invalidate_financial_state()
    {
        var book = await CapacityReservationIntegrationTests.FundedBook();
        var events = new PortfolioEventStore(fixture.EventSourceDb, new PortfolioAuthorityFence(Transactions()));
        var changed = new PortfolioOperatingStateChangedEvent(Guid.NewGuid(), Guid.NewGuid(), 10, DateTime.UtcNow, "integration", PortfolioOperatingState.Disabled, "stale write");
        await FluentActions.Awaiting(() => events.AppendPortfolioAsync(new(book.PortfolioId), changed, 9)).Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.ConcurrencyException>();
        var snapshot = await new FinancialQueryStore(Transactions()).ReadAsync(new FinancialReadScope { PortfolioId = book.PortfolioId, Access = new("integration", ["PortfolioAdministrator"]) }, new GetAccountBalancesRequest());
        snapshot.FinancialRevision.Should().Be(1); snapshot.Value!.OperatingState.Should().Be("Active");
    }
}
