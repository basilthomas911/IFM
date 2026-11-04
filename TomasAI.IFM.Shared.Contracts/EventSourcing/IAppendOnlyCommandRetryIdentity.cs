namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Opts into retries across appended nullable evidence fields. Existing fields must
/// retain their exact encoding; unknown stored fields remain a conflict. Owners
/// must validate legacy JSON duplicates before accepting or replaying them.
/// </summary>
public interface IAppendOnlyCommandRetryIdentity : ICommandRetryIdentity;
