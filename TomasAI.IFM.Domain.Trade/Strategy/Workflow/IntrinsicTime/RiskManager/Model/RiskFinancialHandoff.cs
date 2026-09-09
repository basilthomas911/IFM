using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Deterministic financial request construction and immutable-result matching.</summary>
public static class RiskFinancialHandoff
{
    public static Guid Identity(Guid invocation, string purpose)
        =>new(SHA256.HashData(Encoding.UTF8.GetBytes($"RiskFinancial/v1/{invocation:N}/{purpose}")).AsSpan(0,16));

    public static AdvanceRiskFinancialHandoffCommand Advance(IntrinsicTimeStrategyWorkflowView view)
    {
        var phase=view.FinancialHandoff?.Phase ?? RiskFinancialHandoffPhase.None;
        return new()
        {
            CommandId=Identity(view.RiskExecution!.CommandId,phase==RiskFinancialHandoffPhase.FundPending ? "Execution" : $"Advance/{phase}"),
            Subject=new(ActorType.Command,AdvanceRiskFinancialHandoffCommand.Actor,AdvanceRiskFinancialHandoffCommand.Verb,view.EntityId.Format()),
            EntityId=view.EntityId,WorkflowId=view.WorkflowId,InputWorkflowRevision=view.WorkflowRevision,ExpectedPhase=phase
        };
    }

    public static ReservePortfolioTradeRiskCommand Reserve(IntrinsicTimeStrategyWorkflowView view,
        RiskAssessmentResult result, FinancialRead<FinancialAdmissionSnapshot> read, DateTime now)
    {
        var snapshot=read.Value;
        Require(result.Outcome==RiskAssessmentOutcome.Approved && result.Requirements is not null && result.MarginEvidence is not null &&
            snapshot is { CanPrepareAdmission:true,MigrationQualified:true,OperatingState:"Active" } &&
            snapshot.PortfolioId==result.PortfolioId && snapshot.FundId==result.FundId && snapshot.Authority==result.Authority &&
            snapshot.Environment==result.Environment && now<result.ValidUntilUtc && read.Status==FinancialReadStatus.Found,
            "RM.HANDOFF.AUTHORITY");
        var operation=Identity(result.InvocationId,"Reserve"); var entity=new FinancialExecutionId(result.PortfolioId,operation);
        var request=new ReservePortfolioTradeRiskCommand
        {
            CommandId=operation,OperationId=operation,EntityId=entity,PortfolioId=result.PortfolioId,
            Subject=new(ActorType.Function,ReservePortfolioTradeRiskCommand.Actor,ReservePortfolioTradeRiskCommand.Verb,entity.Format()),
            CorrelationId=view.CorrelationId,CausationId=view.RiskManagement.SourceEventId,
            RequestedAtUtc=now,ExpiresAtUtc=result.ValidUntilUtc,ExpectedFinancialRevision=read.FinancialRevision,
            Access=new("IntrinsicTimeStrategyWorkflow",["CapacityReserve"],[result.PortfolioId]),
            Body=new()
            {
                ReservationId=Identity(result.InvocationId,"Reservation"),FundId=result.FundId,BookId=snapshot!.BookId,
                OrderId=checked((int)result.OrderId),TradeIds=result.Legs.Select(x=>checked((int)x.TradeId)).Distinct().Order().ToArray(),
                WorkflowId=result.WorkflowId.Value,InputWorkflowRevision=result.InputWorkflowRevision,RiskInvocationId=result.InvocationId,
                RiskResultId=result.ResultId,RiskAssessmentHash=RiskContracts.Hash(result),CompositionResultId=result.CompositionResultId,
                CompositionResultHash=result.CompositionResultHash,UnitCandidateHash=result.UnitCandidateHash,SizedOrderHash=result.SizedOrderHash,
                StrategyUnits=result.StrategyUnits,Requirements=result.Requirements!,Authority=result.Authority,
                MarginEvidenceReference=result.MarginEvidence!,ExecutionEnvironment=result.Environment,ValidUntilUtc=result.ValidUntilUtc,
                AcceptedIntentReference=Identity(result.InvocationId,"Execution").ToString("N")
            }
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }

    public static FundRiskAuthorizationReference Authorize(ReservePortfolioTradeRiskCommand request,CapacityReservationCompletedEvent completed)
    {
        var r=completed.Receipt; var b=request.Body;
        Require(completed.OperationId==request.OperationId && completed.InputHash==request.InputSha256 && completed.Id==r.CompletedEventId &&
            r.OperationId==request.OperationId && r.PortfolioId==request.PortfolioId && r.FundId==b.FundId && r.BookId==b.BookId && r.OrderId==b.OrderId &&
            r.ReservationId==b.ReservationId && r.RiskResultId==b.RiskResultId && r.RiskAssessmentHash==b.RiskAssessmentHash &&
            r.CompositionResultHash==b.CompositionResultHash && r.UnitCandidateHash==b.UnitCandidateHash && r.SizedOrderHash==b.SizedOrderHash &&
            r.StrategyUnits==b.StrategyUnits && r.Requirements.ContentHash==b.Requirements.ContentHash &&
            FinancialCanonicalHash.Requirements(r.Requirements)==b.Requirements.ContentHash && r.ValidUntilUtc==b.ValidUntilUtc &&
            r.ExecutionEnvironment==b.ExecutionEnvironment && r.AuthorityEpoch==b.Authority.AuthorityEpoch && r.TradeIds.SequenceEqual(b.TradeIds),
            "RM.HANDOFF.RESERVATION_MISMATCH");
        return FundRiskAuthorizationReference.From(b.RiskInvocationId,b.WorkflowId,r);
    }

    public static ConsumeCapacityReservationCommand Consume(IntrinsicTimeStrategyWorkflowView view,CapacityExecutionAcceptance intent,
        long financialRevision,Guid sourceEventId,DateTime now)
    {
        var operation=Identity(view.RiskExecution!.CommandId,"Consume"); var entity=new FinancialExecutionId(intent.PortfolioId,operation);
        var request=new ConsumeCapacityReservationCommand
        {
            CommandId=operation,OperationId=operation,EntityId=entity,PortfolioId=intent.PortfolioId,
            Subject=new(ActorType.Function,ConsumeCapacityReservationCommand.Actor,ConsumeCapacityReservationCommand.Verb,entity.Format()),
            CorrelationId=view.CorrelationId,CausationId=sourceEventId,RequestedAtUtc=now,ExpiresAtUtc=intent.ValidUntilUtc,
            ExpectedFinancialRevision=financialRevision,Access=new("IntrinsicTimeStrategyWorkflow",["CapacityConsume"],[intent.PortfolioId]),
            Body=new()
            {
                ReservationId=intent.ReservationId,ExpectedReservationVersion=1,ChangeKind=CapacityChangeKind.Consume,
                ExecutionId=intent.ExecutionId,ExecutionRevision=intent.ExecutionRevision,RemainingUnits=view.FinancialHandoff!.Authorization!.StrategyUnits,
                ExpectedRequirementsHash=intent.RequirementsHash,Source=new()
                {
                    System="IntrinsicTimeStrategyWorkflow",SourceEntityId=view.WorkflowId.ToString(),SourceEventId=sourceEventId,
                    SourceSequence=intent.ExecutionRevision,SourceContentHash=FinancialCanonicalHash.Compute(intent),OccurredAtUtc=now,OrderId=intent.OrderId
                }
            }
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }

    public static FinancialExecutionOrder Order(IntrinsicTimeStrategyWorkflowView view,Guid executionId)
    {
        var risk=view.RiskManagement.Result!.RiskResult!;
        var candidate=view.RiskExecution!.CompositionResult.ReadCompositionResult().Candidate!;
        var order=new FinancialExecutionOrder
        {
            ExecutionId=executionId,PortfolioId=risk.PortfolioId,FundId=risk.FundId,BookId=view.FinancialHandoff!.ReservationRequest.Body.BookId,
            OrderId=checked((int)risk.OrderId),ReservationId=view.FinancialHandoff.Authorization!.ReservationId,SizedOrderHash=risk.SizedOrderHash,
            StrategyUnits=risk.StrategyUnits,Environment=risk.Environment,ValidUntilUtc=risk.ValidUntilUtc,
            SignedDebitPerUnit=candidate.ExecutionEnvelope.ProposedSignedDebit,
            EntryFees=view.RiskExecution.Funding.Single(x=>x.StrategyUnits==risk.StrategyUnits).EntryFees,
            Legs=risk.Legs.Select(sized=>
            {
                var leg=candidate.Legs.Single(x=>x.InstrumentId==sized.InstrumentId);
                return new FinancialExecutionLeg(checked((int)sized.TradeId),leg.InstrumentId,leg.RawSymbol,leg.InstrumentClass,
                    sized.Side,sized.Contracts,leg.Multiplier);
            }).ToArray()
        };
        return order with { ContentHash=order.Hash() };
    }

    public static SubmitEmulatorOrderCommand Submit(IntrinsicTimeStrategyWorkflowView view,CapacityConsumptionCompletedEvent consumption)
    {
        var operation=Identity(view.RiskExecution!.CommandId,"Submit"); var handoff=view.FinancialHandoff!;
        var entity=new LedgerPortfolioId(handoff.Order!.PortfolioId);
        var request=new SubmitEmulatorOrderCommand
        {
            CommandId=operation,OperationId=operation,PortfolioId=entity.PortfolioId,EntityId=entity,
            Subject=new(ActorType.Command,SubmitEmulatorOrderCommand.Actor,SubmitEmulatorOrderCommand.Verb,entity.Format()),
            CorrelationId=view.CorrelationId,CausationId=consumption.Id,RequestedAtUtc=consumption.CommittedAtUtc,
            ExpiresAtUtc=handoff.Order.ValidUntilUtc,ExpectedFinancialRevision=consumption.Receipt.FinancialRevision,
            Access=new("IntrinsicTimeStrategyWorkflow",["EmulatorSubmit"],[entity.PortfolioId]),
            Body=new(handoff.Order,consumption.OperationId,consumption.Id,consumption.InputHash)
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }

    static void Require(bool value,string reason)=>RiskUnitModel.Require(value,reason);
}
