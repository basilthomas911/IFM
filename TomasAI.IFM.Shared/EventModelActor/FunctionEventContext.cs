using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Local input to a Function terminal-event factory; this is not a serialized actor message.</summary>
/// <typeparam name="TRequest">The Function's command contract.</typeparam>
/// <param name="EventType">The exact completed or failed CLR event type to construct.</param>
/// <param name="Request">The decoded command, or null when parsing failed.</param>
/// <param name="Outcome">Optional domain calculation outcome interpreted by the mapped handler.</param>
/// <param name="Exception">The exception raised by the Function lifecycle, when applicable.</param>
/// <param name="Stage">The lifecycle stage that failed.</param>
/// <param name="IsConflict">Whether existing completed state conflicts with the incoming request.</param>
public sealed record FunctionEventContext<TRequest>(
    Type EventType,
    TRequest? Request,
    object? Outcome = null,
    Exception? Exception = null,
    FunctionFailureStage Stage = FunctionFailureStage.Unknown,
    bool IsConflict = false)
    where TRequest : class, ICommand;
