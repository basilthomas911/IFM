namespace TomasAI.IFM.UI.Net.Services.Operations;

/// <summary>Represents the transport-neutral outcome of a UI service operation.</summary>
public sealed class UiOperationResult
{
    UiOperationResult(UiOperationError? error) => Error = error;

    /// <summary>Gets whether the operation completed successfully.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Gets the failure, or <see langword="null"/> when the operation succeeded.</summary>
    public UiOperationError? Error { get; }

    /// <summary>Creates a successful operation result.</summary>
    public static UiOperationResult Success() => new(null);

    /// <summary>Creates a failed operation result.</summary>
    /// <param name="code">The stable service or client error code.</param>
    /// <param name="message">The safe error message suitable for presentation.</param>
    public static UiOperationResult Failure(int code, string message)
        => new(new UiOperationError(code, message ?? string.Empty));
}

/// <summary>Represents the transport-neutral outcome and value of a UI service operation.</summary>
/// <typeparam name="TValue">The UI-owned value returned on success.</typeparam>
public sealed class UiOperationResult<TValue>
{
    UiOperationResult(TValue? value, UiOperationError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>Gets whether the operation completed successfully.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Gets the successful value, or its default when the operation failed.</summary>
    public TValue? Value { get; }

    /// <summary>Gets the failure, or <see langword="null"/> when the operation succeeded.</summary>
    public UiOperationError? Error { get; }

    /// <summary>Creates a successful operation result.</summary>
    /// <param name="value">The UI-owned result value.</param>
    public static UiOperationResult<TValue> Success(TValue value)
        => new(value, null);

    /// <summary>Creates a failed operation result.</summary>
    /// <param name="code">The stable service or client error code.</param>
    /// <param name="message">The safe error message suitable for presentation.</param>
    public static UiOperationResult<TValue> Failure(int code, string message)
        => new(default, new UiOperationError(code, message ?? string.Empty));
}
