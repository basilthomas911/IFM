using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Intrinsic parameter mutation identifiers shared by command boundaries.</summary>
public static class ParameterIdentityValidation
{
    /// <summary>Checks the parameter set's intrinsic identity.</summary>
    public static List<ValidationError> ValidateParameterSetId(this List<ValidationError> errors, ParameterSetEntityId id)
    {
        if (id.SetId == Guid.Empty) errors.Add(new("ParameterSet.EntityId.SetId is required."));
        return errors;
    }

    /// <summary>Checks the parameter assignment's intrinsic identity.</summary>
    public static List<ValidationError> ValidateParameterAssignmentId(this List<ValidationError> errors, ParameterAssignmentEntityId id)
    {
        if (id.AssignmentId == Guid.Empty) errors.Add(new("ParameterAssignment.EntityId.AssignmentId is required."));
        return errors;
    }

    /// <summary>Checks the singleton startup registry identity.</summary>
    public static List<ValidationError> ValidateParameterStartupId(this List<ValidationError> errors, ParameterStartupEntityId id)
    {
        if (id != ParameterStartupEntityId.Registry) errors.Add(new("ParameterStartup.EntityId must be the startup registry."));
        return errors;
    }

}
