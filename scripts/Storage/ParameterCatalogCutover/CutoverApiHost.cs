using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Runs the actual API entry point against Development data with private broker/cache and no background writers.</summary>
sealed class CutoverApiHost:IAsyncDisposable
{
    readonly KestrelWebApplicationFactory<ApiServerEntryPoint> source=new();
    KestrelWebApplicationFactory<ApiServerEntryPoint>? host;
    HttpClient? client;
    public static async Task<CutoverApiHost> StartAsync(string connection,CancellationToken token)
    {
        var owner=new CutoverApiHost();
        try
        {
            var nats=Environment.GetEnvironmentVariable("IFM_PARAMETER_CUTOVER_NATS")??throw new InvalidOperationException("A private cutover NATS endpoint is required.");
            var redis=Environment.GetEnvironmentVariable("IFM_PARAMETER_CUTOVER_REDIS")??throw new InvalidOperationException("A private cutover Redis endpoint is required.");
            if(!nats.StartsWith("nats://127.0.0.1:",StringComparison.Ordinal)||!redis.StartsWith("127.0.0.1:",StringComparison.Ordinal))
                throw new InvalidOperationException("Cutover transport endpoints must be private loopback listeners.");
            owner.host=owner.source.WithWebHostBuilder(builder=>
            {
                builder.UseEnvironment("Development")
                    .UseContentRoot(Path.GetFullPath("TomasAI.IFM.Application.Api.Server"))
                    .UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Reference")
                    .UseSetting("IFM_TEST_POSTGRES_CONNECTION",connection)
                    .UseSetting("IFM_TEST_NATS_URL",nats)
                    .UseSetting("IFM_TEST_REDIS_URL",redis)
                    .UseSetting("IntrinsicTimeStrategyWorkflow:ProvisionDevelopmentMarketConditionAssessmentDefaults","false");
                builder.ConfigureServices(services=>services.RemoveAll<IHostedService>());
            });
            owner.client=owner.host.CreateClient();
            if(owner.client.BaseAddress!.Port==0)throw new InvalidOperationException("API did not bind a real Kestrel listener.");
            while(true)
            {
                using var response=await owner.client.GetAsync("/health/actors",token);
                if(response.IsSuccessStatusCode)break;
                await Task.Delay(100,token);
            }
            Console.WriteLine("Real Development API actor host is ready on an isolated Kestrel listener.");
            return owner;
        }
        catch{await owner.DisposeAsync();throw;}
    }
    public async ValueTask DisposeAsync()
    {
        var errors=new List<Exception>();
        async Task Attempt(Func<Task> action){try{await action();}catch(Exception error){errors.Add(error);}}
        if(host is not null)await Attempt(()=>host.Services.GetRequiredService<IActorSupervisor>().ShutdownAsync().AsTask());
        client?.Dispose();
        if(host is not null)await Attempt(()=>host.DisposeAsync().AsTask());
        await Attempt(()=>source.DisposeAsync().AsTask());
        if(errors.Count!=0)throw new AggregateException("Cutover API verification cleanup failed",errors);
    }
}
