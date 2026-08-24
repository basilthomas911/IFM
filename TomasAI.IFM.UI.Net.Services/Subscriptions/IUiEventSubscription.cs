namespace TomasAI.IFM.UI.Net.Services.Subscriptions;

/// <summary>Owns one idempotent backend-event subscription for a presentation workflow.</summary>
public interface IUiEventSubscription : IAsyncDisposable
{
    /// <summary>Gets whether the subscription has started successfully.</summary>
    bool IsStarted { get; }

    /// <summary>Starts the subscription once; repeated calls while started have no effect.</summary>
    /// <param name="cancellationToken">Cancels startup before ownership is established.</param>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the subscription once; repeated calls while stopped have no effect.</summary>
    /// <param name="cancellationToken">Cancels the stop request before cleanup starts.</param>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
