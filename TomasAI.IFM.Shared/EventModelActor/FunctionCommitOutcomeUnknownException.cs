namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>The caller must reconcile the original operation before assuming rollback or retrying.</summary>
public sealed class FunctionCommitOutcomeUnknownException(string message, Exception? innerException = null)
    : Exception(message, innerException);
