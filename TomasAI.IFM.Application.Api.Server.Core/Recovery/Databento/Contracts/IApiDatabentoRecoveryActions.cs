using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Contracts;

/// <summary>Evidence owned exclusively by one accepted hard-reset recovery call.</summary>
public sealed class ApiDatabentoRecoveryContext(DatabentoHardRecoveryRequest request)
{
    /// <summary>Identifies the caller, value date, and reason for this recovery.</summary>
    public DatabentoHardRecoveryRequest Request { get; } = request;
    /// <summary>Frozen in-memory subscription manifests used to start replacement workers.</summary>
    public DatasetRecoveryManifestSnapshot? Frozen { get; set; }
    /// <summary>Whether local qualification must prove active market-data progress.</summary>
    public bool LiveTrading { get; set; }
    /// <summary>Exact replacement generation for each required dataset.</summary>
    public Dictionary<string, Guid> Generations { get; } = new(StringComparer.Ordinal);
    /// <summary>Current bounded worker replacement attempt.</summary>
    public int Attempt { get; set; } = 1;
    /// <summary>Local qualification evidence, retained when a later action fails.</summary>
    public DatabentoHardRecoveryResult? HardResult { get; set; }
}

/// <summary>
/// Individual recovery actions. Every action must succeed or throw with failure details.
/// An action may perform bounded retries internally, but cannot orchestrate another full reset.
/// </summary>
public interface IApiDatabentoRecoveryActions
{
    /// <summary>Freezes and validates the in-memory recovery manifests and market session.</summary>
    Task CaptureRecoveryInputsAsync(ApiDatabentoRecoveryContext context, CancellationToken token);
    /// <summary>Fences existing generations and verifies that outstanding publication is isolated.</summary>
    Task FenceFailedGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token);
    /// <summary>Stops or kills the owned workers and proves their process and handler containment.</summary>
    Task StopDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token);
    /// <summary>Starts replacement workers using only the frozen subscription manifests.</summary>
    Task StartDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token);
    /// <summary>Proves exact-generation local Databento health within its finite deadline.</summary>
    Task QualifyDatabentoAsync(ApiDatabentoRecoveryContext context, CancellationToken token);
    /// <summary>Ensures the isolated downstream publisher is running.</summary>
    Task StartPublisherAsync(ApiDatabentoRecoveryContext context, CancellationToken token);
    /// <summary>Admits all qualified replacement generations and releases held publications.</summary>
    Task AdmitGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token);
    /// <summary>Attempts to publish the fatal recovery reason to the user-visible System Console.</summary>
    Task NotifySystemConsoleAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure);
    /// <summary>Requests the API's bounded fatal shutdown using the original recovery evidence.</summary>
    Task ShutdownApiAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure);
}
