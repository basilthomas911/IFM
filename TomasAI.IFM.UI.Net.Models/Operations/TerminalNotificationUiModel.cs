namespace TomasAI.IFM.UI.Net.Models.Operations;

/// <summary>Identifies the transport-neutral operation represented by a terminal notification.</summary>
public enum TerminalNotificationKind
{
    /// <summary>The operation kind is not relevant or was not supplied.</summary>
    Unknown,

    /// <summary>An entity was added.</summary>
    Added,

    /// <summary>An entity was changed.</summary>
    Changed,

    /// <summary>An entity was removed.</summary>
    Removed,

    /// <summary>A collection was imported.</summary>
    Imported
}

/// <summary>Represents a transport-neutral terminal command notification.</summary>
/// <param name="CommandId">The accepted command correlation identifier.</param>
/// <param name="ErrorCode">The failure code, or zero for successful completion.</param>
/// <param name="ErrorMessage">The safe failure message, or an empty string for success.</param>
/// <param name="Kind">The UI-owned operation kind.</param>
public sealed record TerminalNotificationUiModel(
    Guid CommandId,
    int ErrorCode = 0,
    string ErrorMessage = "",
    TerminalNotificationKind Kind = TerminalNotificationKind.Unknown)
{
    /// <summary>Gets whether the terminal notification reports a failure.</summary>
    public bool IsFailure => ErrorCode != 0;
}
