using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Adapts parameter payload rules to the common actor validation list.</summary>
public static class RegimeDiscoveryParameterSetValidationExtensions
{

    extension(List<ValidationError> errors)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
        public List<ValidationError> ValidateRegimeDiscoveryParameterSet(
        RegimeDiscoveryParameterSet? value)
        {
            ArgumentNullException.ThrowIfNull(errors);
            errors.AddRange(new RegimeDiscoveryParameterSetValidationRules().Execute(value!));
            return errors;
        }
    }
}
