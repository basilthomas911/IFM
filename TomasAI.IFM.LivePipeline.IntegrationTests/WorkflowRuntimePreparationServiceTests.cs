using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class WorkflowRuntimePreparationServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lifecycle_preparation_precedes_an_earlier_registered_source_even_with_concurrent_host_start(bool fail)
    {
        var gate=new TaskCompletionSource<WorkflowRuntimePreparation.Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparation=new WorkflowRuntimePreparation(_=>gate.Task);
        var source=new SourceObserver(preparation);
        var service=new WorkflowRuntimePreparationService(preparation,new(){LocalDecisionRuntimeEnabled=true},NullLogger<WorkflowRuntimePreparationService>.Instance);
        using var host=new HostBuilder().ConfigureServices(services=>
        {
            services.Configure<HostOptions>(options=>options.ServicesStartConcurrently=true);
            services.AddSingleton<IHostedService>(source); // Intentionally earlier than preparation.
            services.AddSingleton<IHostedService>(service);
        }).Build();
        var starting=host.StartAsync();
        Assert.False(source.Started);Assert.False(preparation.IsReady);
        Assert.Equal(HealthStatus.Unhealthy,(await service.CheckHealthAsync(new HealthCheckContext())).Status);
        if(fail)
        {
            gate.SetException(new IOException("startup serialization failure"));
            await Assert.ThrowsAsync<IOException>(()=>starting);
            Assert.False(source.Started);Assert.False(preparation.IsReady);
            Assert.Equal(HealthStatus.Unhealthy,(await service.CheckHealthAsync(new HealthCheckContext())).Status);
        }
        else
        {
            gate.SetResult(new(1,1,TimeSpan.Zero));await starting;
            Assert.True(source.Started);Assert.True(source.WasPrepared);
            Assert.Equal(HealthStatus.Healthy,(await service.CheckHealthAsync(new HealthCheckContext())).Status);
            await host.StopAsync();
        }
    }
    [Fact]
    public async Task Host_start_waits_for_real_code_preparation_before_reporting_ready()
    {
        var preparation=new WorkflowRuntimePreparation(new TomasAI.IFM.Domain.Portfolio.OrderComposition.Model.PortfolioEntryDecisionCalculator());
        var service=new WorkflowRuntimePreparationService(preparation,new(){LocalDecisionRuntimeEnabled=true},
            NullLogger<WorkflowRuntimePreparationService>.Instance);
        await service.StartAsync(default);
        Assert.True(preparation.IsReady);Assert.NotNull(preparation.Prepared);
        Assert.True(preparation.Prepared.PreparedMethods>0);Assert.True(preparation.Prepared.PreparedContracts>0);
        Assert.Equal(HealthStatus.Healthy,(await service.CheckHealthAsync(new HealthCheckContext())).Status);
    }
    [Fact]
    public async Task Enabled_local_runtime_is_unhealthy_before_startup_preparation_finishes()
    {
        var preparation=new WorkflowRuntimePreparation();
        var service=new WorkflowRuntimePreparationService(preparation,new(){LocalDecisionRuntimeEnabled=true},
            NullLogger<WorkflowRuntimePreparationService>.Instance);
        var health=await service.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy,health.Status);Assert.False(preparation.IsReady);
        Assert.Contains("not completed",health.Description);Assert.Null(preparation.Prepared);
    }
    [Fact]
    public async Task Disabled_local_runtime_does_not_run_code_preparation_or_change_activation()
    {
        var preparation=new WorkflowRuntimePreparation();var options=new IntrinsicTimeStrategyWorkflowOptions();
        var service=new WorkflowRuntimePreparationService(preparation,options,NullLogger<WorkflowRuntimePreparationService>.Instance);
        await service.StartAsync(default);
        Assert.Equal(HealthStatus.Healthy,(await service.CheckHealthAsync(new HealthCheckContext())).Status);
        Assert.Null(preparation.Prepared);Assert.False(preparation.IsReady);Assert.False(options.Enabled);Assert.False(options.LocalDecisionRuntimeEnabled);
    }
    sealed class SourceObserver(WorkflowRuntimePreparation preparation):IHostedService
    {
        public bool Started;public bool WasPrepared;
        public Task StartAsync(CancellationToken token){Started=true;WasPrepared=preparation.IsReady;return Task.CompletedTask;}
        public Task StopAsync(CancellationToken token)=>Task.CompletedTask;
    }
}
