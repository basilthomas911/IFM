namespace TomasAI.IFM.UI.Net.Services.Operations;

/// <summary>Reports a failed UI service result to a presentation workflow.</summary>
public sealed class UiOperationException : Exception
{
    /// <summary>Creates an exception from a stable UI operation error.</summary>
    /// <param name="error">The operation error.</param>
    public UiOperationException(UiOperationError error)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message)
        => ErrorCode = error.Code;

    /// <summary>Gets the stable service or client error code.</summary>
    public int ErrorCode { get; }
}

/// <summary>Provides result-unwrapping helpers for presentation workflow code.</summary>
public static class UiOperationResultExtensions
{
    /// <summary>Returns the successful value or throws a typed UI operation exception.</summary>
    /// <typeparam name="TValue">The UI-owned value type.</typeparam>
    /// <param name="result">The operation result to unwrap.</param>
    public static TValue RequireValue<TValue>(this UiOperationResult<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess || result.Value is null)
            throw new UiOperationException(result.Error ?? new UiOperationError(0, "The operation failed."));
        return result.Value;
    }
}
