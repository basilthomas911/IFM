using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Freezes server-resolved policy, coherent financial revision and explicit emulator quotes into one durable invocation.</summary>
public static class RiskPreparation
{
    public static ExecuteRiskManagementPipelineCommand Create(IntrinsicTimeStrategyWorkflowView view, RiskParameterSet parameters,
        FinancialRead<FinancialAdmissionSnapshot> read, Guid invocationId, DateTime now)
    {
        parameters.Validate();
        RiskUnitModel.Require(view is { Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.RiskManagement }
            && view.RiskExecution is null && invocationId!=Guid.Empty && now.Kind==DateTimeKind.Utc,
            "RM.PREPARATION.IDENTITY");
        var composition=view.OrderComposition.Result?.ReadCompositionResult() ?? throw new RiskCalculationException("RM.INPUT.NO_COMPOSITION");
        var candidate=composition.Candidate ?? throw new RiskCalculationException("RM.INPUT.NO_CANDIDATE");
        var market=view.CompositionExecution?.MarketSnapshot ?? throw new RiskCalculationException("RM.INPUT.NO_SNAPSHOT");
        var financial=read.Value;
        RiskUnitModel.Require(read.Status==FinancialReadStatus.Found && read.FinancialRevision>0 && financial is not null
            && financial.CanPrepareAdmission && financial.MigrationQualified && financial.OperatingState=="Active"
            && financial.PortfolioId==candidate.PortfolioId && financial.FundId==candidate.FundId
            && financial.Authority.DeploymentKey==candidate.DeploymentKey && financial.Environment==parameters.Environment
            && read.ObservedAtUtc<=now && (now-read.ObservedAtUtc).TotalSeconds<=1,"RM.AUTHORITY.UNAVAILABLE");
        DateTime expiry=new[] { view.ExpiresAtUtc,candidate.ValidUntilUtc,market.ValidUntilUtc.UtcDateTime,
            financial!.Authority.ValidUntilUtc,view.MarketCondition.Result!.AssessmentResult!.Assessment.ValidUntilUtc
                ?? throw new RiskCalculationException("RM.INPUT.ASSESSMENT_EXPIRY") }.Min();
        RiskUnitModel.Require(expiry>now,"RM.TIME.EXPIRED");
        var fundLoss=financial.Limits.SingleOrDefault(x=>x.ScopeKind==CapacityScopeKind.Fund
            && x.ScopeKey==FinancialScopeKeys.Fund(candidate.FundId) && x.Measure==CapacityMeasure.LossCharge && x.Unit==CapacityUnit.Usd);
        RiskUnitModel.Require(fundLoss is { Enabled:true } && financial.MaximumRiskPerTrade>0,"RM.AUTHORITY.LIMIT_MISSING");
        // This version explicitly uses available settled cash as its conservative capital basis, not unverified NAV.
        var authority=new RiskSizingAuthority(candidate.PortfolioId,candidate.FundId,candidate.DeploymentKey,
            FinancialScopeKeys.Underlying(candidate.Product.Symbol,candidate.Product.Exchange,candidate.Product.Currency),financial.AvailableCash,Math.Max(0,financial.AvailableCash),Math.Min(fundLoss!.Maximum,financial.MaximumRiskPerTrade),
            financial.Limits.ToImmutableArray(),financial.Usage.ToImmutableArray(),now,expiry,financial.Environment);
        var policy=parameters.Sizing(); var revision=checked(view.WorkflowRevision+1);
        var id=new RiskManagementExecutionId(view.EntityId,view.WorkflowId,revision,1);
        var request=new ExecuteRiskManagementPipelineCommand
        {
            CommandId=invocationId,EntityId=id,Subject=new(ActorType.Function,ExecuteRiskManagementPipelineCommand.Actor,
                ExecuteRiskManagementPipelineCommand.Verb,id.Format()),InputWorkflowRevision=revision,
            CorrelationId=view.CorrelationId,CausationId=view.OrderComposition.SourceEventId,RequestedAtUtc=now,
            EvaluatedAtUtc=now,ExpiresAtUtc=expiry,RegimeResult=view.RegimeDiscovery.Result!,MarketConditionResult=view.MarketCondition.Result!,
            SelectionResult=view.TradeSelection.Result!,CompositionResult=view.OrderComposition.Result!,MarketSnapshot=market,
            Policy=policy,PolicyId=parameters.ParameterSetId,PolicyVersion=parameters.Version,
            PolicyHash=RiskContracts.Hash(new { PolicyId=parameters.ParameterSetId,PolicyVersion=(long)parameters.Version,Policy=policy }),
            ConfigurationPayloadSha256=parameters.Hash(),
            SizingAuthority=authority,Authority=financial.Authority,IncrementalLossReserve=parameters.IncrementalLossReserve,
            Funding=EmulatorMarginModel.Quote(parameters,candidate,financial.ExecutionAccountReference,financial.Environment,now,expiry)
        };
        request=request with { InputSha256=request.Fingerprint() };
        RiskUnitModel.Require(new List<ValidationError>().ValidateRiskFields(request).Count==0,"RM.PREPARATION.INVALID");
        return request;
    }
}
