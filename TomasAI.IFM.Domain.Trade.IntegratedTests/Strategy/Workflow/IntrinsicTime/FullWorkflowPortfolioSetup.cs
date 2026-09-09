using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    static async Task InitializeWorkflowPortfolioAsync(IServiceProvider services, TradeSelectionBinding binding)
    {
        await services.GetRequiredService<TomasAI.IFM.Application.Storage.SequenceIdDb.Schema.SequenceIdSchemaDb>().CreateAllAsync();
        await services.GetRequiredService<PortfolioFinancialSchema>().InitializeAsync();
        await services.GetRequiredService<TomasAI.IFM.Application.Storage.PortfolioDb.Schema.PortfolioSchemaDb>().CreateAllAsync();
        var source = binding.PortfolioSnapshot;
        var id = source.Portfolio.PortfolioId;
        var authority = new FinancialAuthorityReference { DeploymentKey = binding.Candidates[0].DeploymentKey, AssignmentVersion = 1,
            PortfolioVersion = 1, FundMandateVersion = 1, PolicyId = 1, PolicyVersion = 1, EnvelopeId = source.RiskEnvelope.EnvelopeId, EnvelopeVersion = 1,
            FinancialSnapshotHash = source.PayloadSha256, AuthorityEpoch = 1, SourceWatermark = "workflow-fixture/1", ValuationWatermark = "workflow-fixture/1", ValidUntilUtc = source.ValidUntilUtc };
        var sizing = new RiskSizingAuthority(id, id, authority.DeploymentKey, FinancialScopeKeys.Underlying("ES", "CME", "USD"),
            1000000000,1000000000,1000000000,[],[],DateTime.UtcNow,source.ValidUntilUtc,"Emulator");
        var requirements = RiskSizingModel.Requirements(new(100,100,100,0,100000,1,1,0,0,0,36), sizing, new(1,10000,10000,5,0,new FinancialEvidenceReference()));
        var limits = requirements.Exposures.Select(x => new CapacityLimit(x.ScopeKind,x.ScopeKey,x.Measure,x.Unit,1000000000)).ToArray();
        var fundLimits = limits.Where(x => x.ScopeKind is CapacityScopeKind.Portfolio or CapacityScopeKind.Fund).ToArray();
        var deploymentLimits = limits.Where(x => x.ScopeKind is CapacityScopeKind.Deployment or CapacityScopeKind.Underlying).ToArray();
        var book = new FinancialBookConfiguration { BookId=id,PortfolioId=id,AccountingEntityId=Guid.NewGuid(),
            ExecutionAccountReference=$"FullWorkflowFixture/{Guid.NewGuid():N}",Environment="Emulator",MigrationQualified=true,
            AuthorityEpoch=1,SourceWatermark=authority.SourceWatermark,ValuationWatermark=authority.ValuationWatermark,
            Funds=[new() { FundId=source.Fund.FundId,CanSpend=true,Reference=authority,PortfolioStreamVersion=1,FundStreamVersion=1,PolicyStreamVersion=1,
                Limits=fundLimits,Deployments=[new(authority,deploymentLimits,1000000000)] }] };
        var transactions = FinancialBoundaryTransactions();
        // Initial immutable authority is test setup, outside the measured workflow. All subsequent
        // Fund transitions, ledger funding, reservation and authorization use production services.
        await transactions.ExecuteAsync(async (db, ct) => {
            var command = Guid.NewGuid();
            await db.AppendAsync($"Portfolio.{id}", command, new PortfolioCreated(Guid.NewGuid(),command,1,DateTime.UtcNow,"FullWorkflowFixture",source.Portfolio),0,ct);
            command = Guid.NewGuid();
            await db.AppendAsync($"PortfolioFund.{id}.{source.Fund.FundId}",command,new FundMandateCreated(Guid.NewGuid(),command,1,DateTime.UtcNow,"FullWorkflowFixture",source.Fund),0,ct);
            command = Guid.NewGuid();
            await db.AppendAsync($"PortfolioFinancialPolicy.{id}.1",command,new PortfolioFinancialPolicyCreated(Guid.NewGuid(),command,1,DateTime.UtcNow,"FullWorkflowFixture",source.FinancialPolicy,Guid.NewGuid()),0,ct);
            return true;
        });
        var rule=new LedgerPostingRule(Guid.NewGuid(),1,"five-stage-funding-rule",LedgerTransactionKind.DepositConfirmed,new(101,1),new(102,1),true);
        await new PortfolioFinancialDbContext(transactions).CreateBookAsync(book,
            [new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity")],[rule],new(2020,1,1),new(2099,12,31));
        var postingId=Guid.NewGuid();var now=DateTime.UtcNow;
        var posting=new PostFundTransactionCommand { CommandId=postingId,OperationId=postingId,PortfolioId=book.PortfolioId,EntityId=new(book.PortfolioId),
            Subject=new(ActorType.Command,PostFundTransactionCommand.Actor,PostFundTransactionCommand.Verb,book.PortfolioId.ToString()),
            CorrelationId=binding.PortfolioSnapshot.CorrelationId,CausationId=postingId,RequestedAtUtc=now,ExpiresAtUtc=now.AddSeconds(10),ExpectedFinancialRevision=0,
            Access=new("FiveStageFundingFixture",["PortfolioAdministrator"]),Body=new() { BookId=book.BookId,FundId=source.Fund.FundId,Amount=1000000000,Currency="USD",
                TransactionKind=LedgerTransactionKind.DepositConfirmed,AccountingDate=DateOnly.FromDateTime(now),ValueDate=DateOnly.FromDateTime(now),
                PostingRule=new() { RuleId=rule.RuleId,Version=1,ContentHash=rule.ContentHash },
                Source=new() { System="FiveStageFundingFixture",SourceEventId=postingId,SourceContentHash=new('F',64),OccurredAtUtc=now },
                MovementEvidence=new() { Status=MovementStatus.Confirmed,SourceReference="Labelled numerical fixture; no actual cash" } } };
        posting=posting with { InputSha256=FinancialCanonicalHash.Request(posting) };
        var funded = await services.GetRequiredService<IPortfolioFinancialApi>().PostAsync(posting);
        funded.Success.Should().BeTrue(funded.ErrorMessage);
    }

    static async Task AssertWorkflowReservationCountAsync(TradeSelectionBinding binding, int expected)
    {
        var count = await FinancialBoundaryTransactions().ExecuteAsync(async (db, ct) =>
            Convert.ToInt32(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.capacity_reservation WHERE portfolio_id=$1;", [binding.PortfolioSnapshot.Portfolio.PortfolioId],ct)));
        count.Should().Be(expected);
    }
}
