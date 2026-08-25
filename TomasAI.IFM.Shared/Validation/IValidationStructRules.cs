namespace TomasAI.IFM.Shared.Validation;

/// <summary>Defines validation rules for a non-nullable value-type contract.</summary>
/// <typeparam name="TValue">The value type validated by the implementation.</typeparam>
public interface IValidationStructRules<TValue> where TValue : struct
{
    /// <summary>Validates the supplied value.</summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    ValidationError[] Execute(TValue value);
}
