namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>
/// Represents a failed UI service result while preserving its application error code.
/// </summary>
public sealed class UiServiceOperationException : Exception
{
    public UiServiceOperationException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the error code returned by the UI service operation.
    /// </summary>
    public int ErrorCode { get; }
}
