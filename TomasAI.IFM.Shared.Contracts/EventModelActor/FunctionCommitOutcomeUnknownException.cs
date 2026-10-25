namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>The caller must reconcile the original operation before assuming rollback or retrying.</summary>
public sealed class FunctionCommitOutcomeUnknownException : Exception
{
    public FunctionCommitOutcomeUnknownException() { }
    public FunctionCommitOutcomeUnknownException(string message) : base(message) { }
    public FunctionCommitOutcomeUnknownException(string message, Exception? innerException) : base(message, innerException) { }
}
