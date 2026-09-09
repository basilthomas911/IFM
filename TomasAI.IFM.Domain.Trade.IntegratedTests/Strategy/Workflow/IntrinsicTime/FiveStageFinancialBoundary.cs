using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    static PostgresEventTransaction FinancialBoundaryTransactions()=>new(new DbConnectionSettings().Add(
        EventSourceActorDbContext.EventSourceActorDbConnection,"Host=localhost;Port=5432;Database=event-source-test-db","System.Data.Postgres"));

    static async Task<FinancialBookConfiguration> FundFinancialBoundaryAsync(ExecuteRiskManagementPipelineCommand risk)
    {
        // Explicitly labelled test authority and funding. No application book or real cash is used.
        var authority=risk.Authority;var candidate=risk.CompositionResult.ReadCompositionResult().Candidate!;
        var book=new FinancialBookConfiguration { BookId=candidate.PortfolioId,PortfolioId=candidate.PortfolioId,AccountingEntityId=Guid.NewGuid(),
            ExecutionAccountReference=$"FiveStageFixture/{Guid.NewGuid():N}",Environment=risk.SizingAuthority.Environment,MigrationQualified=true,
            AuthorityEpoch=authority.AuthorityEpoch,SourceWatermark=authority.SourceWatermark,ValuationWatermark=authority.ValuationWatermark,
            Funds=[new() { FundId=candidate.FundId,CanSpend=true,Reference=authority,PortfolioStreamVersion=1,FundStreamVersion=1,PolicyStreamVersion=1,
                Limits=risk.SizingAuthority.Limits.ToArray() }] };
        var transactions=FinancialBoundaryTransactions();
        await transactions.ExecuteAsync(async(db,ct)=>{
            foreach(var stream in new[] { $"Portfolio.{book.PortfolioId}",$"PortfolioFund.{book.PortfolioId}.{candidate.FundId}",$"PortfolioFinancialPolicy.{book.PortfolioId}.{authority.PolicyId}" })
            {
                var command=Guid.NewGuid();await db.AppendAsync(stream,command,new PortfolioCreated(Guid.NewGuid(),command,1,DateTime.UtcNow,"FiveStageAuthorityFixture",new()),0,ct);
            }
            return true;
        });
        var rule=new LedgerPostingRule(Guid.NewGuid(),1,"five-stage-funding-rule",LedgerTransactionKind.DepositConfirmed,new(101,1),new(102,1),true);
        await new PortfolioFinancialDbContext(transactions).CreateBookAsync(book,
            [new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity")],[rule],new(2020,1,1),new(2099,12,31));
        var id=Guid.NewGuid();var now=DateTime.UtcNow;
        var posting=new PostFundTransactionCommand { CommandId=id,OperationId=id,PortfolioId=book.PortfolioId,EntityId=new(book.PortfolioId),
            Subject=new(ActorType.Command,PostFundTransactionCommand.Actor,PostFundTransactionCommand.Verb,book.PortfolioId.ToString()),
            CorrelationId=risk.CorrelationId,CausationId=id,RequestedAtUtc=now,ExpiresAtUtc=now.AddSeconds(10),ExpectedFinancialRevision=0,
            Access=new("FiveStageFundingFixture",["PortfolioAdministrator"]),Body=new() { BookId=book.BookId,FundId=candidate.FundId,Amount=1000000000,Currency="USD",
                TransactionKind=LedgerTransactionKind.DepositConfirmed,AccountingDate=DateOnly.FromDateTime(now),ValueDate=DateOnly.FromDateTime(now),
                PostingRule=new() { RuleId=rule.RuleId,Version=1,ContentHash=rule.ContentHash },
                Source=new() { System="FiveStageFundingFixture",SourceEventId=id,SourceContentHash=new('F',64),OccurredAtUtc=now },
                MovementEvidence=new() { Status=MovementStatus.Confirmed,SourceReference="Labelled numerical fixture; no actual cash" } } };
        posting=posting with { InputSha256=FinancialCanonicalHash.Request(posting) };
        await new GeneralLedgerStore(transactions).PostAsync(posting,[new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),posting.Body)],
            (item,r,prior,original,remaining)=>LedgerPostingModel.Calculate(item,r,prior,original,remaining),info=>posting.Complete(info));
        return book;
    }

    async Task VerifyCalculatedRiskFinancialBoundaryAsync(ExecuteRiskManagementPipelineCommand request,RiskManagementFunctionCompletedEvent completed,
        IntrinsicTimeStrategyWorkflowView upstream,FinancialBookConfiguration book)
    {
        var risk=completed.Result;var now=DateTime.UtcNow;
        var view=upstream with { RiskExecution=request,RiskManagement=new() { Result=StrategyStageResultEnvelope.CreateRisk(risk),SourceEventId=completed.Id } };
        var snapshot=new FinancialAdmissionSnapshot(book.BookId,book.PortfolioId,risk.FundId,"Active",true,true,risk.Authority,1000000000,book.Funds[0].Limits,[],book.Environment,book.ExecutionAccountReference);
        var reserve=RiskFinancialHandoff.Reserve(view,risk,new(FinancialReadStatus.Found,snapshot,1,now),now);
        var store=new CapacityReservationStore(FinancialBoundaryTransactions());
        var grant=await store.ReserveAsync(reserve,CapacityAdmissionModel.ValidateCommitted,receipt=>new CapacityReservationCompletedEvent {
            Id=receipt.CompletedEventId,EntityId=reserve.EntityId,Subject=reserve.Subject,CommandId=reserve.CommandId,OperationId=reserve.OperationId,
            PortfolioId=reserve.PortfolioId,InputHash=reserve.InputSha256,CommittedAtUtc=receipt.GrantedAtUtc,Receipt=receipt });
        grant.Receipt.RiskResultId.Should().Be(risk.ResultId);grant.Receipt.StrategyUnits.Should().Be(risk.StrategyUnits);
        var authorization=RiskFinancialHandoff.Authorize(reserve,grant);
        var fundEvent=new FundCompositionStateChanged(Guid.NewGuid(),RiskFinancialHandoff.Identity(risk.InvocationId,"Fund"),2,DateTime.UtcNow,"FiveStageFixture",
            new() { PortfolioId=book.PortfolioId,FundId=risk.FundId,OrderId=authorization.OrderId,Status="RiskApproved",RiskAuthorization=authorization });
        await new PortfolioEventStore(database.ActorEventSourceDb,new PortfolioAuthorityFence(FinancialBoundaryTransactions()))
            .AppendFundAsync(new(book.PortfolioId,risk.FundId),fundEvent,1);
        var accepted=await new FinancialQueryStore(FinancialBoundaryTransactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,FundId=risk.FundId,Access=reserve.Access with { Roles=["LedgerRead"] } },new GetFundRiskAuthorizationRequest(fundEvent.CommandId));
        accepted.Value!.Authorization.Should().Be(authorization);
        // Store reload proves these results came from the actual persisted Risk event and one reservation.
        (await new PortfolioFinancialDbContext(FinancialBoundaryTransactions()).ReadOperationAsync<CapacityReservationCompletedEvent>(book.PortfolioId,reserve.OperationId))!.Id.Should().Be(grant.Id);
    }
}
