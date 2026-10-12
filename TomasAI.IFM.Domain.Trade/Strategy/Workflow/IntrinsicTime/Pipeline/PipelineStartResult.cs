namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;
/// <summary>Typed, side-effect-free outcome of an operator's initialization boundary.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "These typed result factories preserve the established pipeline-start API.")]
public sealed record PipelineStartResult<T>(bool Success, T? Value, PipelineInitializationError? Error)
{
    public static PipelineStartResult<T> Started(T value) => new(true, value, null);
    public PipelineStartResult<T> WithParameterSet(Guid parameterSetId, int parameterSetVersion, string parameterPayloadSha256) => Error is null ? this : this with
    {
        Error = Error with
        {
            ParameterSetId = parameterSetId,
            ParameterSetVersion = parameterSetVersion,
            ParameterPayloadSha256 = parameterPayloadSha256 ?? string.Empty
        }
    };
    public static PipelineStartResult<T> Failed(string code, string type, string message, IReadOnlyDictionary<string, string>? diagnostics = null) => new(false, default, new(code, type, message, [code], diagnostics ?? new Dictionary<string, string>(StringComparer.Ordinal)));
    public static PipelineStartResult<T> Failed(string code, string type, string message, IReadOnlyList<string> reasons, IReadOnlyDictionary<string, string>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(reasons);
        return new(false, default, new(code, type, message, reasons.Count == 0 ? [code] : reasons, diagnostics ?? new Dictionary<string, string>(StringComparer.Ordinal)));
    }
}
