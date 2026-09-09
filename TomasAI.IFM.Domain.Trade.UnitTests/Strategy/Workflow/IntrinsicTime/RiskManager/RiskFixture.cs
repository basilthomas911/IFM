using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

internal static class RiskFixture
{
    internal static async Task<ExecuteRiskManagementPipelineCommand> Command(string variant="LongFuture", DateTime? atUtc=null,string contractId="ESZ6")
    {
        var composed=await CompositionFixture.Command(variant,atUtc:atUtc,contractId:contractId);
        var composition=new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()).Calculate(composed);
        var candidate=composition.Candidate!; var at=composed.EvaluatedAtUtc;
        var expires=candidate.ValidUntilUtc;
        var policy=RiskSizingPolicy.Default(candidate.TargetHorizon); var policyId=Guid.NewGuid(); long policyVersion=1;
        var evidence=new FinancialEvidenceReference { EvidenceId=Guid.NewGuid(),Version=1,ContentHash=new('A',64),Source="EmulatorFixture/v1",
            Environment="Test",ObservedAtUtc=at,ValidUntilUtc=expires };
        var authority=new RiskSizingAuthority(candidate.PortfolioId,candidate.FundId,candidate.DeploymentKey,FinancialScopeKeys.Underlying(candidate.Product.Symbol,candidate.Product.Exchange,candidate.Product.Currency),
            1000000000,1000000000,1000000000,[],[],at,expires,"Test");
        var funding=Enumerable.Range(1,10).Select(q=>new RiskQuantityFunding(q,10000*q,10000*q,5*q,0,evidence)).ToImmutableArray();
        var template=RiskSizingModel.Requirements(new(100,100,100,0,100000,1,1,0,0,0,36),authority,funding[0]);
        authority=authority with { Limits=template.Exposures.Select(x=>new CapacityLimit(x.ScopeKind,x.ScopeKey,x.Measure,x.Unit,1000000000)).ToImmutableArray() };
        var id=new RiskManagementExecutionId(composed.WorkflowEntityId,composed.WorkflowId,6,1);
        var c=new ExecuteRiskManagementPipelineCommand
        {
            CommandId=Guid.NewGuid(),EntityId=id,Subject=new(ActorType.Function,ExecuteRiskManagementPipelineCommand.Actor,ExecuteRiskManagementPipelineCommand.Verb,id.Format()),
            InputWorkflowRevision=6,CorrelationId=composed.CorrelationId,CausationId=composed.CommandId,RequestedAtUtc=at,EvaluatedAtUtc=at,ExpiresAtUtc=expires,
            RegimeResult=composed.WorkflowView.RegimeDiscovery.Result!,MarketConditionResult=composed.WorkflowView.MarketCondition.Result!,
            SelectionResult=composed.AcceptedSelectionEnvelope,CompositionResult=StrategyStageResultEnvelope.CreateComposition(composition),
            MarketSnapshot=composed.MarketSnapshot,Policy=policy,PolicyId=policyId,PolicyVersion=policyVersion,
            PolicyHash=RiskContracts.Hash(new { PolicyId=policyId,PolicyVersion=policyVersion,Policy=policy }),SizingAuthority=authority,Funding=funding,
            Authority=new() { DeploymentKey=candidate.DeploymentKey,AssignmentVersion=candidate.AssignmentVersion,FinancialSnapshotHash=new('B',64),AuthorityEpoch=1,ValidUntilUtc=expires,
                PortfolioVersion=1,FundMandateVersion=1,PolicyId=1,PolicyVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=1,
                SourceWatermark="isolated-fixture-source/1",ValuationWatermark="isolated-fixture-valuation/1" }
        };
        return c with { InputSha256=c.Fingerprint() };
    }
}
