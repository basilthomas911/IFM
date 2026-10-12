using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
/// <summary>Explicit version-one emulator gross-contract schedule. It never supplies live broker authority or spread offsets.</summary>
public static class EmulatorMarginModel
{
    public static ImmutableArray<RiskQuantityFunding> Quote(RiskParameterSet policy, CompositionCandidate candidate, string executionAccount, string environment, DateTime evaluatedAtUtc, DateTime validUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        RiskUnitModel.Require(string.Equals(environment, "Emulator", StringComparison.Ordinal) && string.Equals(environment, policy.Environment, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(executionAccount) && executionAccount.Length <= 128, "RM.MARGIN.ENVIRONMENT");
        RiskUnitModel.Require(evaluatedAtUtc.Kind == DateTimeKind.Utc && validUntilUtc.Kind == DateTimeKind.Utc && validUntilUtc > evaluatedAtUtc && validUntilUtc <= candidate.ValidUntilUtc && candidate.TargetHorizon == policy.TargetHorizon && string.Equals(candidate.Product.Symbol, policy.Root, StringComparison.Ordinal) && string.Equals(candidate.Product.Currency, policy.Currency, StringComparison.Ordinal) && candidate.Legs.Length is 1 or 2 or 4 && string.Equals(candidate.CandidateHash, CompositionHash.Candidate(candidate), StringComparison.Ordinal), "RM.MARGIN.CANDIDATE");
        var contracts = candidate.Legs.Sum(x => x.Ratio);
        RiskUnitModel.Require(contracts > 0 && candidate.Legs.All(x => x.Ratio > 0), "RM.MARGIN.CANDIDATE");
        var output = ImmutableArray.CreateBuilder<RiskQuantityFunding>(policy.MaximumUnits);
        for (var units = 1; units <= policy.MaximumUnits; units++)
        {
            var gross = checked(contracts * units);
            decimal margin = checked(policy.MarginPerGrossContract * gross), fees = checked(policy.FeePerGrossContract * gross), variation = checked(policy.VariationReservePerGrossContract * gross);
            var hash = RiskContracts.Hash(new { PolicyHash = policy.Hash(), candidate.CandidateHash, executionAccount, environment, evaluatedAtUtc, validUntilUtc, units, gross, margin, fees, variation });
            var evidence = new FinancialEvidenceReference
            {
                EvidenceId = new Guid(Convert.FromHexString(hash).AsSpan(0, 16)),
                Version = policy.MarginMethodVersion,
                ContentHash = hash,
                Source = "IBKR-Emulator/GrossContractMargin/v1",
                Environment = environment,
                ObservedAtUtc = evaluatedAtUtc,
                ValidUntilUtc = validUntilUtc
            };
            output.Add(new(units, margin, margin, fees, variation, evidence));
        }

        return output.MoveToImmutable();
    }
}
