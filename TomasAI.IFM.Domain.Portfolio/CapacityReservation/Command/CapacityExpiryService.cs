using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command;

/// <summary>Dispatches persisted expiry Commands, reconciling the original operation before any replacement attempt.</summary>
public sealed class CapacityExpiryService(CapacityExpiryDispatchStore dispatch,IPortfolioFinancialApi api,ILogger<CapacityExpiryService> logger):BackgroundService
{
    public async Task RunOnceAsync(CancellationToken token)
    {
        var pending=await dispatch.PrepareAsync(CapacityExpiryModel.Create,token);
        foreach(var item in pending)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if(await dispatch.ReconcileAsync(item.Request,token)) continue;
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(3));
                await api.ChangeAsync(item.Request,deadline.Token);
                await dispatch.ReconcileAsync(item.Request,token);
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; }
            catch(Exception error) { logger.LogWarning(error,"Capacity expiry operation {OperationId} remains pending; no release is inferred.",item.Request.OperationId); }
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
            catch(Exception error) { logger.LogWarning(error,"Capacity expiry dispatcher is pending; financial obligations are retained."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
