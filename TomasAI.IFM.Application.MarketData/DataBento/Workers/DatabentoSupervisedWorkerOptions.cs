using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatabentoSupervisedWorkerOptions
{
    public required string DotNetHostPath { get; init; }
    public required string WorkerAssemblyPath { get; init; }
    public FeedDeploymentProfile DeploymentProfile { get; init; } = FeedDeploymentProfile.Development;
    public FeedDataSourceMode DataSource { get; init; } = FeedDataSourceMode.Synthetic;
    public SyntheticFeedOptions Synthetic { get; init; } = new();
    public Pricing.OptionPricingRefreshPolicy OptionPricingRefresh { get; init; } = new();
    public TimeSpan HostPublisherStopTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public DatabentoSupervisedWorkerOptions Validate()
    {
        OptionPricingRefresh.Validate();
        if (HostPublisherStopTimeout <= TimeSpan.Zero || HostPublisherStopTimeout > TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("The host publisher stop timeout must be positive and no greater than one minute.");
        if (!Path.IsPathFullyQualified(DotNetHostPath) || !File.Exists(DotNetHostPath))
            throw new InvalidOperationException("The Stage 3 dotnet host path must be an existing absolute file.");
        if (!Path.IsPathFullyQualified(WorkerAssemblyPath) || !File.Exists(WorkerAssemblyPath))
            throw new InvalidOperationException("The Stage 3 worker assembly path must be an existing absolute file.");
        return this;
    }

    /// <summary>Creates one exact worker request from an already resolved in-memory manifest.</summary>
    public DatasetWorkerStartRequest CreateStartRequest(DatasetSubscriptionManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var launch = Validate();
        return new DatasetWorkerStartRequest
        {
            ExecutablePath = launch.DotNetHostPath,
            PrefixArguments = [launch.WorkerAssemblyPath,
                "--deployment-profile", launch.DeploymentProfile.ToString(),
                "--data-source", launch.DataSource.ToString(),
                "--option-pricing-refresh", System.Text.Json.JsonSerializer.Serialize(launch.OptionPricingRefresh),
                "--synthetic-record-count", launch.Synthetic.RecordCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--synthetic-records-per-second", launch.Synthetic.RecordsPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--synthetic-start-sequence", launch.Synthetic.StartSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            Dataset = manifest.Dataset,
            ValueDate = manifest.ValueDate,
            WorkerInstanceId = Guid.NewGuid(),
            GenerationId = Guid.NewGuid(),
            ManifestRevision = manifest.Revision,
            Manifest = manifest
        };
    }
}
