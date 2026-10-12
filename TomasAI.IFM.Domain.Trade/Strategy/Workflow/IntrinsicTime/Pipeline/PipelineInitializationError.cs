namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;
public sealed record PipelineInitializationError(string ErrorCode, string ErrorType, string Message, IReadOnlyList<string> ReasonCodes, IReadOnlyDictionary<string, string> DiagnosticData)
{
    public Guid ParameterSetId { get; init; }
    public int ParameterSetVersion { get; init; }
    public string ParameterPayloadSha256 { get; init; } = string.Empty;
}
