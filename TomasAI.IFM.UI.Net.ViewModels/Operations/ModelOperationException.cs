namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>
/// Represents a failed Model service result while preserving its application error code.
/// </summary>
public sealed class ModelOperationException : Exception
{
    public ModelOperationException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the error code returned by the Model operation.
    /// </summary>
    public int ErrorCode { get; }
}
