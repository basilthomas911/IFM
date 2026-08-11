namespace TomasAI.IFM.UI.Net.ViewModels.Presentation;

/// <summary>
/// Describes one presentation-layer error notification without depending on a UI framework.
/// </summary>
public sealed record PresentationError(
    long Sequence,
    int ErrorCode,
    string Message,
    string Caption);
