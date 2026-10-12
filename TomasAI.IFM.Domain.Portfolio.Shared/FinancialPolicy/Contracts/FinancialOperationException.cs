namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>A classified refusal carries whether this attempt changed financial authority.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "Financial failures must carry an explicit registered reason code and commit disposition; unclassified constructors are intentionally unavailable.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "Financial failures must carry an explicit registered reason code and commit disposition; unclassified constructors are intentionally unavailable.")]
public sealed class FinancialOperationException(int code, string message,
    FinancialCommitDisposition disposition = FinancialCommitDisposition.NotCommitted, Guid? existingOperationId = null)
    : Exception(message)
{
    public int Code { get; } = code;
    public string Reason => FinancialReasons.Name(Code);
    public FinancialCommitDisposition Disposition { get; } = disposition;
    public Guid? ExistingOperationId { get; } = existingOperationId;
}
