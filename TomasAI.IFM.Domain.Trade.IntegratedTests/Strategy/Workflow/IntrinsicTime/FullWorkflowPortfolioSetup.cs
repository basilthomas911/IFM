using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Projection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    static async Task InitializeWorkflowPortfolioAsync(IServiceProvider services, TradeSelectionBinding binding, bool initializeSchema = true)
    {
        if (initializeSchema)
        {
            await services.GetRequiredService<TomasAI.IFM.Application.Storage.SequenceIdDb.Schema.SequenceIdSchemaDb>().CreateAllAsync();
            await services.GetRequiredService<PortfolioFinancialSchema>().InitializeAsync();
            await services.GetRequiredService<TomasAI.IFM.Application.Storage.PortfolioDb.Schema.PortfolioSchemaDb>().CreateAllAsync();
        }
        var source = binding.PortfolioSnapshot;
        var id = source.Portfolio.PortfolioId;
        var authority = new FinancialAuthorityReference
        {
            DeploymentKey = binding.Candidates[0].DeploymentKey,
            AssignmentVersion = 1,
            PortfolioVersion = source.Portfolio.PortfolioVersion,
            FundMandateVersion = source.Fund.FundMandateVersion,
            PolicyId = source.FinancialPolicy.PolicyId,
            PolicyVersion = source.FinancialPolicy.PolicyVersion,
            EnvelopeId = source.RiskEnvelope.EnvelopeId,
            EnvelopeVersion = 1,
            FinancialSnapshotHash = source.PayloadSha256,
            AuthorityEpoch = 1,
            SourceWatermark = "workflow-fixture/1",
            ValuationWatermark = "workflow-fixture/1",
            ValidUntilUtc = source.ValidUntilUtc
        };
        var sizing = new RiskSizingAuthority(id, id, authority.DeploymentKey, FinancialScopeKeys.Underlying("ES", "CME", "USD"),
            1000000000, 1000000000, 1000000000, [], [], DateTime.UtcNow, source.ValidUntilUtc, "Emulator");
        var requirements = RiskSizingModel.Requirements(new(100, 100, 100, 0, 100000, 1, 1, 0, 0, 0, 36), sizing, new(1, 10000, 10000, 5, 0, new FinancialEvidenceReference()));
        var limits = requirements.Exposures.Select(x => new CapacityLimit(x.ScopeKind, x.ScopeKey, x.Measure, x.Unit, 1000000000)).ToArray();
        var fundLimits = limits.Where(x => x.ScopeKind is CapacityScopeKind.Portfolio or CapacityScopeKind.Fund).ToArray();
        var deploymentLimits = limits.Where(x => x.ScopeKind is CapacityScopeKind.Deployment or CapacityScopeKind.Underlying).ToArray();
        var book = new FinancialBookConfiguration
        {
            BookId = id,
            PortfolioId = id,
            AccountingEntityId = Guid.NewGuid(),
            ExecutionAccountReference = $"FullWorkflowFixture/{Guid.NewGuid():N}",
            Environment = "Emulator",
            MigrationQualified = true,
            AuthorityEpoch = 1,
            SourceWatermark = authority.SourceWatermark,
            ValuationWatermark = authority.ValuationWatermark,
            Funds = [new() { FundId=source.Fund.FundId,CanSpend=true,Reference=authority,PortfolioStreamVersion=1,FundStreamVersion=1,PolicyStreamVersion=1,
                Limits=fundLimits,Deployments=[new(authority,deploymentLimits,1000000000)] }]
        };
        var transactions = FinancialBoundaryTransactions();
        // Seed committed authority and its read models together, outside the measured workflow.
        // Subsequent Fund transitions, ledger funding, reservation and authorization use production services.
        await transactions.ExecuteAsync(async (db, ct) =>
        {
            var committedAt = DateTime.UtcNow;
            var portfolio = new PortfolioCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), 1, committedAt, "FullWorkflowFixture", source.Portfolio)
            {
                AggregateId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ProjectionFacts = new() { Revision = 1, UpdatedAtUtc = committedAt, Portfolio = source.Portfolio }
            };
            var fund = new FundMandateCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), 1, committedAt, "FullWorkflowFixture", source.Fund)
            {
                AggregateId = $"{id}.{source.Fund.FundId}",
                ProjectionFacts = new() { Revision = 1, UpdatedAtUtc = committedAt, Fund = source.Fund }
            };
            var policy = new PortfolioFinancialPolicyCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), 1, committedAt, "FullWorkflowFixture", source.FinancialPolicy, Guid.NewGuid())
            {
                AggregateId = $"{id}.{authority.PolicyId}",
                ProjectionFacts = new() { Revision = 1, UpdatedAtUtc = committedAt, Policies = [source.FinancialPolicy with { AggregateRevision = 1 }] }
            };
            await db.AppendAsync(new EnlistedEventAppend($"Portfolio.{portfolio.AggregateId}", portfolio.CommandId, portfolio, 0), ct);
            await db.PersistAcceptedPortfolioAsync(portfolio, portfolio.ProjectionFacts, ct);
            await db.AppendAsync(new EnlistedEventAppend($"PortfolioFund.{fund.AggregateId}", fund.CommandId, fund, 0), ct);
            await db.PersistAcceptedPortfolioAsync(fund, fund.ProjectionFacts, ct);
            await db.AppendAsync(new EnlistedEventAppend($"PortfolioFinancialPolicy.{policy.AggregateId}", policy.CommandId, policy, 0), ct);
            await db.PersistAcceptedPortfolioAsync(policy, policy.ProjectionFacts, ct);
            return true;
        });
        var rule = new LedgerPostingRule(Guid.NewGuid(), 1, "five-stage-funding-rule", LedgerTransactionKind.DepositConfirmed, new(101, 1), new(102, 1), true);
        LedgerPostingRule ExecutionRule(LedgerTransactionKind kind, int debit, int credit) =>
            new(Guid.NewGuid(), 1, $"workflow-opening-{kind}", kind, new(debit, 1), new(credit, 1), true);
        await new PortfolioFinancialStore(transactions).CreateBookAsync(book,
            [new(101, 1, "Cash", PostingSide.Debit, true, "cash"), new(102, 1, "Equity", PostingSide.Credit, true, "equity"),
                new(103, 1, "Commission", PostingSide.Debit, true, "expense"),
                new(104, 1, "Settlement clearing", PostingSide.Debit, true, "clearing"),
                new(105, 1, "Realized PnL", PostingSide.Credit, true, "pnl"),
                new(106, 1, "Valuation asset", PostingSide.Debit, true, "Asset"),
                new(107, 1, "Unrealized PnL", PostingSide.Credit, true, "UnrealizedPnl")],
            [rule, ExecutionRule(LedgerTransactionKind.TradeSettlement, 104, 101), ExecutionRule(LedgerTransactionKind.Commission, 103, 101),
                new(Guid.NewGuid(), 1, "workflow-close-realized", LedgerTransactionKind.RealizedPnl, new(104, 1), new(105, 1), true, new(106, 1), new(107, 1))],
            new(2020, 1, 1), new(2099, 12, 31));
        var postingId = Guid.NewGuid(); var now = DateTime.UtcNow;
        var posting = new PostFundTransactionCommand
        {
            CommandId = postingId,
            OperationId = postingId,
            PortfolioId = book.PortfolioId,
            EntityId = new(book.PortfolioId),
            Subject = new(ActorType.Command, PostFundTransactionCommand.Actor, PostFundTransactionCommand.Verb, book.PortfolioId.ToString()),
            CorrelationId = binding.PortfolioSnapshot.CorrelationId,
            CausationId = postingId,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(10),
            ExpectedFinancialRevision = 0,
            Access = new("FiveStageFundingFixture", ["PortfolioAdministrator"]),
            Body = new()
            {
                BookId = book.BookId,
                FundId = source.Fund.FundId,
                Amount = 1000000000,
                Currency = "USD",
                TransactionKind = LedgerTransactionKind.DepositConfirmed,
                AccountingDate = DateOnly.FromDateTime(now),
                ValueDate = DateOnly.FromDateTime(now),
                PostingRule = new() { RuleId = rule.RuleId, Version = 1, ContentHash = rule.ContentHash },
                Source = new() { System = "FiveStageFundingFixture", SourceEventId = postingId, SourceContentHash = new('F', 64), OccurredAtUtc = now },
                MovementEvidence = new() { Status = MovementStatus.Confirmed, SourceReference = "Labelled numerical fixture; no actual cash" }
            }
        };
        posting = posting with { InputSha256 = FinancialCanonicalHash.Request(posting) };
        var funded = await services.GetRequiredService<IPortfolioFinancialApi>().PostAsync(posting);
        funded.Success.Should().BeTrue(funded.ErrorMessage);
        var selected = binding.Candidates[0];
        var admission = await services.GetRequiredService<IPortfolioFinancialApi>()
            .GetFinancialAdmissionSnapshotAsync(new()
            {
                PortfolioId = id,
                FundId = source.Fund.FundId,
                Access = new("FullWorkflowFixture", ["LedgerRead"], [id])
            },
                new(selected.DeploymentKey,
                    FinancialScopeKeys.Underlying(selected.Product.Symbol, selected.Product.Exchange, selected.Product.Currency)));
        admission.Success.Should().BeTrue(admission.ErrorMessage);
    }

    static async Task AssertWorkflowReservationCountAsync(TradeSelectionBinding binding, int expected)
    {
        var count = await FinancialBoundaryTransactions().ExecuteAsync(async (db, ct) =>
            Convert.ToInt32(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.capacity_reservation WHERE portfolio_id=$1;", [binding.PortfolioSnapshot.Portfolio.PortfolioId], ct)));
        count.Should().Be(expected);
    }
}
