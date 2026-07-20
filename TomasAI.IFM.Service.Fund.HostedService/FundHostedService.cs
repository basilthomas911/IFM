using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.Fund.HostedService;

public class FundHostedService(IFundEventConsumer fundEventConsumer)
    : IHostedService
{
    public async Task StartAsync(CancellationToken stoppingToken)
        => await fundEventConsumer.StartAsync();

    public async Task StopAsync(CancellationToken cancellationToken) 
        => await fundEventConsumer.StopAsync();
}
