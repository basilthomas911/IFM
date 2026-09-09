using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-04")]
[Collection("PortfolioFinancialDatabase")]
public sealed class CapacityReservationIntegrationTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    static CapacityReservationStore Store()=>new(Transactions());

    [Fact]
    public async Task Posted_entry_fee_clears_only_its_exact_consumed_execution_hold_without_double_counting_cash()
    {
        var book=await FundedBook();
        var reserve=await ReserveRequest(book,700,requirements=>requirements with { SettlementCash=0,FeeReserve=700 });
        await Reserve(reserve);
        var consume=ConsumeRequest(reserve,2); await Accept(reserve,consume); await Consume(consume);
        var fee=Request(book,LedgerTransactionKind.Commission,100,3);
        fee=fee with { Body=fee.Body with { CapacityReservationId=reserve.Body.ReservationId,FundingComponent=CapacityFundingComponent.EntryFees,
            Source=fee.Body.Source with { OrderId=reserve.Body.OrderId,TradeId=reserve.Body.TradeIds[0],SourceEntityId=consume.Body.ExecutionId.ToString("N") } } };
        fee=fee with { InputSha256=FinancialCanonicalHash.Request(fee) };
        await Post(fee); await Post(fee);
        var balance=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,FundId=reserve.Body.FundId,Access=reserve.Access },new GetAccountBalancesRequest());
        balance.Value!.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(900);
        balance.Value.AvailableCash.Should().Be(300); balance.Value.OperatingState.Should().Be("Active");
        var bad=Request(book,LedgerTransactionKind.Commission,100,4);
        bad=bad with { Body=fee.Body with { Source=fee.Body.Source with { SourceEventId=Guid.NewGuid(),SourceEntityId=Guid.NewGuid().ToString("N") } } };
        bad=bad with { InputSha256=FinancialCanonicalHash.Request(bad) };
        (await FluentActions.Awaiting(()=>Post(bad)).Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        (await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,FundId=reserve.Body.FundId,Access=reserve.Access },new GetAccountBalancesRequest()))
            .Value!.AvailableCash.Should().Be(300);
    }

    [Fact]
    public async Task Partial_fill_followed_by_confirmed_cancel_retains_a_whole_position_slot()
    {
        var book=await CreateBook(b=>b with { Funds=[b.Funds[0] with { Limits=[
            new(CapacityScopeKind.Portfolio,b.PortfolioId.ToString(),CapacityMeasure.LossCharge,CapacityUnit.Usd,1000),
            new(CapacityScopeKind.Portfolio,b.PortfolioId.ToString(),CapacityMeasure.PositionSlots,CapacityUnit.Positions,1)] }] });
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1000,0));
        var reserve=await ReserveRequest(book,700,requirements=>requirements with { Exposures=[..requirements.Exposures,
            new() { ScopeKind=CapacityScopeKind.Portfolio,ScopeKey=book.PortfolioId.ToString(),Measure=CapacityMeasure.PositionSlots,Unit=CapacityUnit.Positions,Amount=1,MethodVersion=1 }] });
        await Reserve(reserve); var consume=ConsumeRequest(reserve,2); await Accept(reserve,consume); await Consume(consume);
        await Change(ChangeRequest(consume,CapacityChangeKind.RecordFill,3,2,4,0,6));
        var cancel=ChangeRequest(consume,CapacityChangeKind.ConfirmCancel,4,3,4,6,0);
        cancel=cancel with { Body=cancel.Body with { RelatedPostingReference="reconciliation:fixture" } };
        cancel=cancel with { InputSha256=FinancialCanonicalHash.Request(cancel) };
        await Transactions().ExecuteAsync((db,ct)=>db.AppendAsync($"IntegrationReconciliation.{cancel.Body.Source.SourceEventId:N}",cancel.Body.Source.SourceEventId,
            new TestCapacityReconciliationEvent { Id=Guid.NewGuid(),CommandId=cancel.Body.Source.SourceEventId,CapacityReconciliation=new()
            { ExecutionId=consume.Body.ExecutionId,ExecutionRevision=cancel.Body.ExecutionRevision,PortfolioId=book.PortfolioId,FundId=reserve.Body.FundId,
                OrderId=reserve.Body.OrderId,ReservationId=reserve.Body.ReservationId,SourceContentHash=cancel.Body.Source.SourceContentHash,FilledUnits=4,CancelledUnits=6,
                FinancialFactsComplete=true,ReconciliationReference=cancel.Body.RelatedPostingReference,Environment=book.Environment } },0,ct));
        await Change(cancel);
        var usage=await Transactions().ExecuteAsync((db,ct)=>db.QueryAsync("SELECT held,working,position FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1 AND measure=$2;",
            [book.PortfolioId,(int)CapacityMeasure.PositionSlots],r=>(r.GetDecimal(0),r.GetDecimal(1),r.GetDecimal(2)),ct));
        usage.Single().Should().Be((0m,0m,1m));
    }

    [Fact]
    public async Task Competing_reservations_cannot_spend_the_same_cash_and_replay_preserves_original_receipt()
    {
        var book=await FundedBook(); var first=await ReserveRequest(book,700); var second=await ReserveRequest(book,700);
        async Task<CapacityReservationCompletedEvent?> Try(ReservePortfolioTradeRiskCommand request)
        {
            try { return await Reserve(request); }
            catch(FinancialOperationException ex) when(ex.Code==FinancialReasons.RevisionConflict) { return null; }
        }
        var results=await Task.WhenAll(Try(first),Try(second)); results.Count(x=>x is not null).Should().Be(1);
        var winner=results[0] is not null?first:second; var original=results.Single(x=>x is not null)!;
        var replay=await Reserve(winner); replay.Id.Should().Be(original.Id); replay.Receipt.FinancialRevision.Should().Be(2);
        var loser=(results[0] is null?first:second) with { ExpectedFinancialRevision=2,OperationId=Guid.NewGuid() };
        loser=loser with { InputSha256=FinancialCanonicalHash.Request(loser) };
        var error=await FluentActions.Awaiting(()=>Reserve(loser)).Should().ThrowAsync<FinancialOperationException>();
        error.Which.Code.Should().Be(FinancialReasons.InsufficientCash);
        (await Usage(book)).Held.Should().Be(700);
    }

    [Fact]
    public async Task Caller_requirement_vector_cannot_replace_committed_risk_assessment()
    {
        var book=await FundedBook(); var request=await ReserveRequest(book,700);
        var forged=request.Body.Requirements with { SettlementCash=1 };
        forged=forged with { ContentHash=CapacityAdmissionModel.Hash(forged) };
        request=request with { Body=request.Body with { Requirements=forged } };
        request=request with { InputSha256=FinancialCanonicalHash.Request(request) };
        var error=await FluentActions.Awaiting(()=>Reserve(request)).Should().ThrowAsync<FinancialOperationException>();
        error.Which.Code.Should().Be(FinancialReasons.RequestMismatch);
        (await Store().ReadCurrentAsync(book.PortfolioId,request.Body.ReservationId)).Should().BeNull();
    }

    [Fact]
    public async Task Consumption_requires_committed_matching_acceptance_then_retains_capacity_when_submission_unknown()
    {
        var book=await FundedBook(); var reserve=await ReserveRequest(book,700); await Reserve(reserve);
        var consume=ConsumeRequest(reserve,2); var refused=await FluentActions.Awaiting(()=>Consume(consume)).Should().ThrowAsync<FinancialOperationException>();
        refused.Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        await Accept(reserve,consume); var result=await Consume(consume);
        result.Receipt.Status.Should().Be(ReservationStatus.Consumed);
        var unknown=ChangeRequest(consume,CapacityChangeKind.MarkSubmissionUnknown,3,2,0,0,10);
        await Change(unknown);
        var current=await Store().ReadCurrentAsync(book.PortfolioId,reserve.Body.ReservationId);
        current!.Status.Should().Be(ReservationStatus.SubmissionUnknown);
        (await Usage(book)).Should().Be((0m,700m,0m));
        var replay=await Consume(consume); replay.Id.Should().Be(result.Id);
        (await Store().ReadCurrentAsync(book.PortfolioId,reserve.Body.ReservationId))!.Status.Should().Be(ReservationStatus.SubmissionUnknown);
    }

    [Fact]
    public async Task Changed_portfolio_source_blocks_consumption_of_previously_valid_reservation()
    {
        var book=await FundedBook(); var reserve=await ReserveRequest(book,700); await Reserve(reserve);
        var consume=ConsumeRequest(reserve,2); await Accept(reserve,consume);
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("UPDATE event_stream_id SET currentversion=currentversion+1 WHERE eventstream=$1;",[$"Portfolio.{book.PortfolioId}"],ct);
            return true;
        });
        var refusal=await FluentActions.Awaiting(()=>Consume(consume)).Should().ThrowAsync<FinancialOperationException>();
        refusal.Which.Code.Should().Be(FinancialReasons.AuthorityRevoked);
        (await Usage(book)).Should().Be((700m,0m,0m));
    }

    [Fact]
    public async Task Partial_fill_moves_only_filled_risk_to_position_and_lifecycle_command_cannot_consume()
    {
        var book=await FundedBook(); var reserve=await ReserveRequest(book,700); await Reserve(reserve);
        var consume=ConsumeRequest(reserve,2);
        var wrongRoute=ChangeRequest(consume,CapacityChangeKind.Consume,2,1,0,0,10);
        var refused=await FluentActions.Awaiting(()=>Change(wrongRoute)).Should().ThrowAsync<FinancialOperationException>();
        refused.Which.Code.Should().Be(FinancialReasons.InvalidLifecycle);
        await Accept(reserve,consume); await Consume(consume);
        await Change(ChangeRequest(consume,CapacityChangeKind.RecordFill,3,2,4,0,6));
        (await Usage(book)).Should().Be((0m,420m,280m));
        var expired=ChangeRequest(consume,CapacityChangeKind.ExpireUnconsumed,4,3,4,6,0);
        var failure=await FluentActions.Awaiting(()=>Change(expired)).Should().ThrowAsync<FinancialOperationException>();
        failure.Which.Code.Should().Be(FinancialReasons.InvalidLifecycle);
        (await Usage(book)).Should().Be((0m,420m,280m));
    }

    [Fact]
    public async Task Unconsumed_release_removes_hold_without_rewriting_original_reservation_receipt()
    {
        var book=await FundedBook(); var reserve=await ReserveRequest(book,700); var original=await Reserve(reserve);
        var placeholder=ConsumeRequest(reserve,2);
        var release=ChangeRequest(placeholder,CapacityChangeKind.ReleaseUnconsumed,2,1,0,10,0);
        release=release with { Body=release.Body with { ExecutionId=Guid.Empty,ExecutionRevision=0 } };
        release=release with { InputSha256=FinancialCanonicalHash.Request(release) };
        await Change(release); (await Usage(book)).Should().Be((0m,0m,0m));
        (await Reserve(reserve)).Id.Should().Be(original.Id);
        (await Store().ReadCurrentAsync(book.PortfolioId,reserve.Body.ReservationId))!.Status.Should().Be(ReservationStatus.Released);
    }

    internal static async Task<FinancialBookConfiguration> FundedBook()
    {
        var book=await CreateBook(b=>b with { Funds=[b.Funds[0] with { Limits=[new(CapacityScopeKind.Portfolio,b.PortfolioId.ToString(),CapacityMeasure.LossCharge,CapacityUnit.Usd,1000)] }] });
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1000,0)); return book;
    }

    internal static async Task<ReservePortfolioTradeRiskCommand> ReserveRequest(FinancialBookConfiguration book,decimal funding,Func<CapacityRequirements,CapacityRequirements>? configure=null,TimeSpan? validity=null)
    {
        var now=DateTime.UtcNow; var operation=Guid.NewGuid(); var risk=Guid.NewGuid();
        var requirements=new CapacityRequirements { Currency="USD",SettlementCash=funding,LossCharge=funding,GrossContracts=10,PositionSlots=1,AccountingMethodVersion=1,
            Exposures=[new() { ScopeKind=CapacityScopeKind.Portfolio,ScopeKey=book.PortfolioId.ToString(),Measure=CapacityMeasure.LossCharge,Unit=CapacityUnit.Usd,Amount=funding,MethodVersion=1 }] };
        if(configure is not null) requirements=configure(requirements);
        requirements=requirements with { ContentHash=CapacityAdmissionModel.Hash(requirements) };
        var body=new CapacityReservationRequest { ReservationId=Guid.NewGuid(),BookId=book.BookId,FundId=book.Funds[0].FundId,
            OrderId=Random.Shared.Next(1,1000000000),TradeIds=[1],WorkflowId=Guid.NewGuid(),InputWorkflowRevision=4,
            RiskInvocationId=risk,RiskResultId=Guid.NewGuid(),RiskAssessmentHash=new string('A',64),CompositionResultId=Guid.NewGuid(),
            CompositionResultHash=new string('B',64),UnitCandidateHash=new string('C',64),SizedOrderHash=new string('D',64),StrategyUnits=10,
            Requirements=requirements,Authority=book.Funds[0].Reference,ExecutionEnvironment=book.Environment,ValidUntilUtc=now.Add(validity??TimeSpan.FromMinutes(1)),
            MarginEvidenceReference=new() { EvidenceId=Guid.NewGuid(),Version=1,ContentHash=new string('E',64),Source="IntegrationEvidence",Environment=book.Environment,ObservedAtUtc=now,ValidUntilUtc=now.AddMinutes(2) } };
        var assessment=new QualifiedCapacityAssessment { InvocationId=risk,ResultId=body.RiskResultId,ResultHash=body.RiskAssessmentHash,
            PortfolioId=book.PortfolioId,FundId=body.FundId,WorkflowId=body.WorkflowId,WorkflowRevision=body.InputWorkflowRevision,
            CompositionResultId=body.CompositionResultId,CompositionResultHash=body.CompositionResultHash,UnitCandidateHash=body.UnitCandidateHash,
            SizedOrderHash=body.SizedOrderHash,StrategyUnits=body.StrategyUnits,Requirements=requirements,Authority=body.Authority,
            MarginEvidence=body.MarginEvidenceReference,Environment=body.ExecutionEnvironment,ValidUntilUtc=body.ValidUntilUtc,Eligible=true };
        await Transactions().ExecuteAsync((db,ct)=>db.AppendAsync($"IntegrationRisk.{risk:N}",risk,
            new TestCapacityAssessmentEvent { Id=Guid.NewGuid(),CommandId=risk,CapacityAssessment=assessment,ReceivedOn=now },0,ct));
        var request=new ReservePortfolioTradeRiskCommand { CommandId=operation,OperationId=operation,PortfolioId=book.PortfolioId,EntityId=new(book.PortfolioId,operation),
            Subject=new(ActorType.Function,ReservePortfolioTradeRiskCommand.Actor,ReservePortfolioTradeRiskCommand.Verb,new FinancialExecutionId(book.PortfolioId,operation).Format()),
            CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(1),ExpectedFinancialRevision=1,Body=body,Access=new("integration",["PortfolioAdministrator"]) };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }

    internal static Task<CapacityReservationCompletedEvent> Reserve(ReservePortfolioTradeRiskCommand request)=>Store().ReserveAsync(request,CapacityAdmissionModel.ValidateCommitted,
        receipt=>new() { Id=receipt.CompletedEventId,EntityId=request.EntityId,Subject=request.Subject,CommandId=request.CommandId,
            OperationId=request.OperationId,PortfolioId=request.PortfolioId,InputHash=request.InputSha256,CommittedAtUtc=receipt.GrantedAtUtc,Receipt=receipt });
    internal static ConsumeCapacityReservationCommand ConsumeRequest(ReservePortfolioTradeRiskCommand reserve,long revision)
    {
        var operation=Guid.NewGuid(); var now=DateTime.UtcNow;
        var request=new ConsumeCapacityReservationCommand { CommandId=operation,OperationId=operation,PortfolioId=reserve.PortfolioId,EntityId=new(reserve.PortfolioId,operation),
            Subject=new(ActorType.Function,ConsumeCapacityReservationCommand.Actor,ConsumeCapacityReservationCommand.Verb,new FinancialExecutionId(reserve.PortfolioId,operation).Format()),
            CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(1),ExpectedFinancialRevision=revision,Access=reserve.Access,
            Body=new() { ReservationId=reserve.Body.ReservationId,ExpectedReservationVersion=1,ChangeKind=CapacityChangeKind.Consume,ExecutionId=Guid.NewGuid(),ExecutionRevision=1,
                Source=new() { System="Integration",SourceEventId=Guid.NewGuid(),SourceContentHash=new string('F',64) },RemainingUnits=10,ExpectedRequirementsHash=reserve.Body.Requirements.ContentHash } };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
    internal static Task<CapacityConsumptionCompletedEvent> Consume(ConsumeCapacityReservationCommand request)=>Store().ChangeAsync(request,true,CapacityLifecycleModel.Apply,
        receipt=>new CapacityConsumptionCompletedEvent { Id=receipt.CompletedEventId,EntityId=request.EntityId,Subject=request.Subject,CommandId=request.CommandId,
            OperationId=request.OperationId,PortfolioId=request.PortfolioId,InputHash=request.InputSha256,CommittedAtUtc=receipt.CommittedAtUtc,Receipt=receipt });
    internal static ChangeCapacityReservationCommand ChangeRequest(ConsumeCapacityReservationCommand consume,CapacityChangeKind kind,long revision,long version,int filled,int cancelled,int remaining)
    {
        var operation=Guid.NewGuid(); var body=consume.Body with { ChangeKind=kind,ExpectedReservationVersion=version,FilledUnits=filled,CancelledUnits=cancelled,RemainingUnits=remaining,
            Source=consume.Body.Source with { SourceEventId=Guid.NewGuid() } };
        var entity=new CapacityReservationEntityId(consume.PortfolioId,body.ReservationId);
        var request=new ChangeCapacityReservationCommand { CommandId=operation,OperationId=operation,PortfolioId=consume.PortfolioId,EntityId=entity,
            Subject=new(ActorType.Command,ChangeCapacityReservationCommand.Actor,ChangeCapacityReservationCommand.Verb,entity.Format()),
            CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),RequestedAtUtc=consume.RequestedAtUtc,ExpiresAtUtc=consume.ExpiresAtUtc,ExpectedFinancialRevision=revision,Access=consume.Access,Body=body };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
    internal static Task<CapacityLifecycleCompletedEvent> Change(ChangeCapacityReservationCommand request)=>Store().ChangeAsync(request,false,CapacityLifecycleModel.Apply,
        receipt=>new CapacityLifecycleCompletedEvent { Id=receipt.CompletedEventId,EntityId=request.EntityId,Subject=request.Subject,CommandId=request.CommandId,
            OperationId=request.OperationId,PortfolioId=request.PortfolioId,InputHash=request.InputSha256,CommittedAtUtc=receipt.CommittedAtUtc,Receipt=receipt });
    internal static Task Accept(ReservePortfolioTradeRiskCommand reserve,ConsumeCapacityReservationCommand consume,string executionOrderHash="")=>Transactions().ExecuteAsync((db,ct)=>db.AppendAsync(
        $"IntegrationAcceptance.{consume.Body.ExecutionId:N}",consume.Body.ExecutionId,new TestCapacityAcceptanceEvent { Id=Guid.NewGuid(),CommandId=consume.Body.ExecutionId,
            CapacityAcceptance=new() { ExecutionId=consume.Body.ExecutionId,ExecutionRevision=consume.Body.ExecutionRevision,PortfolioId=reserve.PortfolioId,FundId=reserve.Body.FundId,
                OrderId=reserve.Body.OrderId,ReservationId=reserve.Body.ReservationId,SizedOrderHash=reserve.Body.SizedOrderHash,RequirementsHash=reserve.Body.Requirements.ContentHash,
                Environment=reserve.Body.ExecutionEnvironment,ValidUntilUtc=reserve.Body.ValidUntilUtc,ExecutionOrderHash=executionOrderHash } },0,ct));
    internal static Task<(decimal Held,decimal Working,decimal Position)> Usage(FinancialBookConfiguration book)=>Transactions().ExecuteAsync(async(db,ct)=>
        (await db.QueryAsync("SELECT held,working,position FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1;",[book.PortfolioId],r=>(r.GetDecimal(0),r.GetDecimal(1),r.GetDecimal(2)),ct)).Single());
}

// Real PostgreSQL boundary fixtures only; these are not evidence that the production Risk/workflow producers are qualified.
public record TestFinancialEvidenceEvent : IEvent
{
    public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    public Guid Id { get; init; }
    public long EventId { get; init; }
    public Guid CommandId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = "Integration";
    public DateTime ReceivedOn { get; init; }
    public string UserName=>"Integration";
    public string EventName=>GetType().Name;
    public EventType EventType=>EventType.CompletedEvent;
}
public sealed record TestCapacityAssessmentEvent : TestFinancialEvidenceEvent,ICapacityAssessmentCompletedEvent
{
    public QualifiedCapacityAssessment CapacityAssessment { get; init; }=new();
}
public sealed record TestCapacityAcceptanceEvent : TestFinancialEvidenceEvent,ICapacityExecutionAcceptedEvent
{
    public CapacityExecutionAcceptance CapacityAcceptance { get; init; }=new();
}
public sealed record TestCapacityReconciliationEvent : TestFinancialEvidenceEvent,ICapacityExecutionReconciledEvent
{
    public CapacityExecutionReconciliation CapacityReconciliation { get; init; }=new();
}
