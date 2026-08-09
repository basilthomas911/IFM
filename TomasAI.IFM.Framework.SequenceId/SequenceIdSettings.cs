namespace TomasAI.IFM.Framework.SequenceId;

/// <summary>
/// Shared PostgreSQL sequence allocation settings.
/// </summary>
public static class SequenceIdSettings
{
    /// <summary>
    /// Number of identifiers reserved by each PostgreSQL sequence call.
    /// </summary>
    public const int AllocationSize = 100;
}
