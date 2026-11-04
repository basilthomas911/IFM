using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Intrinsic parameter mutation identifiers shared by command boundaries.</summary>
public static class ParameterIdentityValidation
{

    extension(List<ValidationError> errors)
    {
        /// <summary>Checks the parameter set's intrinsic identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "The serialized collection contract retains its concrete collection shape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retain the published collection contract used by callers and serialization.")]
        public List<ValidationError> ValidateParameterSetId(ParameterSetEntityId id)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (id.SetId == Guid.Empty) errors.Add(new("ParameterSet.EntityId.SetId is required."));
            return errors;
        }

        /// <summary>Checks the parameter assignment's intrinsic identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "The serialized collection contract retains its concrete collection shape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retain the published collection contract used by callers and serialization.")]
        public List<ValidationError> ValidateParameterAssignmentId(ParameterAssignmentEntityId id)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (id.AssignmentId == Guid.Empty) errors.Add(new("ParameterAssignment.EntityId.AssignmentId is required."));
            return errors;
        }

        /// <summary>Checks the per-run startup identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "The serialized collection contract retains its concrete collection shape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retain the published collection contract used by callers and serialization.")]
        public List<ValidationError> ValidateParameterStartupId(ParameterStartupEntityId id)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (id.Id == Guid.Empty || id == ParameterStartupEntityId.Registry) errors.Add(new("ParameterStartup.EntityId must identify one run."));
            return errors;
        }
    }

}
