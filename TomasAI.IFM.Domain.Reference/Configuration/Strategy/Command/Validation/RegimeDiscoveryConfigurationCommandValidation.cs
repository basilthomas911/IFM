using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Validation;

/// <summary>Pure validation of Regime Discovery versioned configuration commands.</summary>
public static class RegimeDiscoveryConfigurationCommandValidation
{
    /// <summary>Checks the parameter-set identity and positive version.</summary>
    public static List<ValidationError> ValidateRegimeDiscoveryConfigurationId(this List<ValidationError> errors, RegimeDiscoveryParameterSetEntityId id)
    {
        if (id.ParameterSetId == Guid.Empty) errors.Add(new("RegimeDiscovery.ParameterSetId is required."));
        if (id.Version <= 0) errors.Add(new("RegimeDiscovery.Version must be positive."));
        return errors;
    }

    /// <summary>Checks all parameter payload rules, creator and payload identity.</summary>
    public static List<ValidationError> ValidateRegimeDiscoveryCreation(this List<ValidationError> errors, CreateRegimeDiscoveryParameterSetCommand command)
    {
        if (command.ParameterSet is null) errors.Add(new("RegimeDiscovery.ParameterSet is required."));
        else
        {
            errors.AddRange(new RegimeDiscoveryParameterSetValidationRules().Execute(command.ParameterSet));
            if (command.EntityId.ParameterSetId != command.ParameterSet.ParameterSetId || command.EntityId.Version != command.ParameterSet.Version)
                errors.Add(new("RegimeDiscovery.EntityId must match ParameterSet identity."));
        }
        if (string.IsNullOrWhiteSpace(command.CreatedBy)) errors.Add(new("RegimeDiscovery.CreatedBy is required."));
        return errors;
    }
}
