using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Common;

/// <summary>Identifies the authenticated caller and Portfolio roles presented to an actor.</summary>
[MessagePackObject]
public sealed record PortfolioAccessContext
{
    [Key(0)] public string Principal { get; init; } = string.Empty;
    [Key(1)] public string[] Roles { get; init; } = [];

    /// <summary>Creates read-only Portfolio access.</summary>
    /// <param name="principal">The authenticated caller identity.</param>
    /// <returns>The caller access context.</returns>
    public static PortfolioAccessContext Reader(string principal) => new() { Principal = principal, Roles = ["PortfolioReader"] };

    /// <summary>Creates Portfolio administrator access.</summary>
    /// <param name="principal">The authenticated caller identity.</param>
    /// <returns>The caller access context.</returns>
    public static PortfolioAccessContext Administrator(string principal) => new() { Principal = principal, Roles = ["PortfolioAdministrator"] };

    /// <summary>Creates strategy-workflow Portfolio access.</summary>
    /// <param name="principal">The authenticated caller identity.</param>
    /// <returns>The caller access context.</returns>
    public static PortfolioAccessContext Workflow(string principal) => new() { Principal = principal, Roles = ["StrategyWorkflow"] };
}

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
