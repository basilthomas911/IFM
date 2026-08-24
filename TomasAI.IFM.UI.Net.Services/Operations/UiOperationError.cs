namespace TomasAI.IFM.UI.Net.Services.Operations;

/// <summary>Describes a transport-neutral failure returned to presentation code.</summary>
/// <param name="Code">The stable service or client error code.</param>
/// <param name="Message">The safe error message suitable for presentation.</param>
public sealed record UiOperationError(int Code, string Message);
