using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Contracts;
using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.HardRecovery;
using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.SoftRecovery;
using TomasAI.IFM.Application.Api.Server.Core.Recovery.Shutdown;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Composition;

/// <summary>Finite local worker deadlines for one hard recovery attempt.</summary>
public sealed record ApiDatabentoHardRuntimePolicy
{
    public TimeSpan MaximumManifestAge { get; init; } = TimeSpan.FromMinutes(6);
    public TimeSpan ContainmentTimeout { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan QualificationTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan MaximumInputAge { get; init; } = TimeSpan.FromSeconds(10);

    public ApiDatabentoHardRuntimePolicy Validate()
    {
        if (MaximumManifestAge <= TimeSpan.Zero || MaximumManifestAge > TimeSpan.FromMinutes(15)
            || ContainmentTimeout <= TimeSpan.Zero || ContainmentTimeout > TimeSpan.FromMinutes(2)
            || QualificationTimeout <= TimeSpan.Zero || QualificationTimeout > TimeSpan.FromMinutes(3)
            || MaximumInputAge <= TimeSpan.Zero || MaximumInputAge > TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("Hard runtime deadlines must be finite and bounded.");
        return this;
    }
}

/// <summary>Composes the one host-owned hard/soft recovery pipeline from its qualified boundaries.</summary>
public static class ApiDatabentoRecoveryComposition
{
    /// <summary>Creates the host-owned coordinator with the production recovery actions.</summary>
    /// <param name="services">The API host's service provider.</param>
    /// <returns>The single sequential recovery coordinator.</returns>
    public static IDatabentoRecoveryRequester Create(IServiceProvider services)
    {
        var actions = CreateActions(services);
        return new ApiDatabentoRecoveryPipeline(actions,
            services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping,
            services.GetRequiredService<ApiDatabentoRecoveryPipelinePolicy>(),
            services.GetRequiredService<ILogger<ApiDatabentoRecoveryPipeline>>());
    }

    /// <summary>Composes the real recovery actions, including direct generation admission and fatal shutdown.</summary>
    /// <param name="services">The API host's service provider.</param>
    /// <returns>Production actions sharing the host's workers, publisher and admission authority.</returns>
    public static IApiDatabentoRecoveryActions CreateActions(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var time = TimeProvider.System;
        var desired = services.GetRequiredService<DatasetDesiredSubscriptionRegistry>();
        var workers = services.GetRequiredService<DatasetWorkerProcessRecoveryService>();
        var sessions = services.GetRequiredService<IFuturesMarketSessionAuthority>();
        var publisher = services.GetRequiredService<ITickAggregationEventPublisher>();
        var diagnostics = publisher as ITickAggregationPublisherDiagnostics
            ?? throw new InvalidOperationException("The supervised publisher must expose generation-isolation diagnostics.");
        var hardRuntime = new SupervisedDatabentoHardRecoveryRuntime(workers,
            services.GetRequiredService<DatabentoSupervisedWorkerOptions>(), time, diagnostics);
        var runtimePolicy = services.GetRequiredService<ApiDatabentoHardRuntimePolicy>();
        var fatal = services.GetRequiredService<IApiFatalRecoveryShutdown>();
        var pipelinePolicy = services.GetRequiredService<ApiDatabentoRecoveryPipelinePolicy>();
        return new ApiDatabentoRecoveryActions(
            desired, workers, services.GetRequiredService<DatasetWorkerAdmissionRegistry>(),
            sessions, services.GetRequiredService<DatabentoSupervisedWorkerOptions>(),
            hardRuntime, services.GetRequiredService<IDatabentoLifecycleRuntime>() as SupervisedDatabentoLifecycleRuntime
                ?? throw new InvalidOperationException("Hard recovery requires the supervised lifecycle owner."),
            runtimePolicy, pipelinePolicy, publisher, diagnostics,
            services.GetRequiredService<TomasAI.IFM.Shared.StatusConsole.ServiceApi.IStatusConsoleWriter>(),
            fatal, time);
    }
}
