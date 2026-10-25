using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Common;

/// <summary>Provides the authenticated Portfolio caller context for the current asynchronous flow.</summary>
public static class PortfolioAccessScope
{
    static readonly AsyncLocal<PortfolioAccessContext?> CurrentValue = new();

    /// <summary>Gets the current caller context, if one was established.</summary>
    public static PortfolioAccessContext? Current => CurrentValue.Value;

    /// <summary>Establishes a caller context until the returned scope is disposed.</summary>
    /// <param name="access">The caller context.</param>
    /// <returns>A scope that restores the previous context.</returns>
    public static IDisposable Push(PortfolioAccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        var prior = CurrentValue.Value;
        CurrentValue.Value = access;
        return new Scope(() => CurrentValue.Value = prior);
    }

    sealed class Scope(Action dispose) : IDisposable
    {
        Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
