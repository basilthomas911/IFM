using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Recovery;

/// <summary>Joins an existing recovery instead of rejecting or starting a competing replacement.
/// The caller's deadline covers all inspection, waiting and confirmed restart work.</summary>
public static class OptionWorkerReadiness
{
    public static async Task EnsureAsync(Func<CancellationToken, Task<MarketDataDatasetStatus?>> observe,
        Func<CancellationToken, Task> restartStopped, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var state = await observe(token).WaitAsync(token).ConfigureAwait(false);
            if (state is { Lifecycle: "Healthy", Running: true, Healthy: true }) return;
            if (state?.Lifecycle == "StoppedError")
            {
                await restartStopped(token).WaitAsync(token).ConfigureAwait(false);
                continue;
            }
            if (state?.Lifecycle != "Recovering")
                throw new InvalidOperationException($"Options feed is not ready; state={state?.Lifecycle ?? "Unavailable"}. "
                    + (state?.Error ?? "No confirmed option worker is available. Saved live-feed intent is retained."));
            // Never hold the backend lifecycle lock while joining worker recovery.
            await Task.Delay(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(false);
        }
    }
}
