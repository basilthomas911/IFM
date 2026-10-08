namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;
/// <summary>Associates native tool output with the current backup operation without transporting artifacts in events.</summary>
public sealed class DatabaseRecoveryOutputScope : IDisposable
{
    static readonly AsyncLocal<Action<string>?> Current = new();
    readonly Action<string>? _previous;
    /// <summary>Installs a bounded-output sink for this async execution context.</summary>
    public DatabaseRecoveryOutputScope(Action<string> append) { _previous = Current.Value; Current.Value = append; }
    /// <summary>Appends diagnostic output when an operation sink is installed.</summary>
    public static void Append(string text) => Current.Value?.Invoke(text);
    /// <summary>Restores the enclosing operation sink.</summary>
    public void Dispose() => Current.Value = _previous;
}
