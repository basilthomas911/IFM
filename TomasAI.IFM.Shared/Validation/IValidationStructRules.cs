namespace TomasAI.IFM.Shared.Validation;

/// <summary>Defines validation rules for a non-nullable value-type contract.</summary>
/// <typeparam name="TValue">The value type validated by the implementation.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3246", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IValidationStructRules<TValue> where TValue : struct
{
    /// <summary>Validates the supplied value.</summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    ValidationError[] Execute(TValue value);
}
