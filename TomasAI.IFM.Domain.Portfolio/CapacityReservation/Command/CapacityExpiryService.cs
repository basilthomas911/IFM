using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command;
/// <summary>Dispatches persisted expiry Commands, reconciling the original operation before any replacement attempt.</summary>
public sealed class CapacityExpiryService(CapacityExpiryDispatchStore dispatch, IPortfolioFinancialApi api, ILogger<CapacityExpiryService> logger) : BackgroundService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "S3267", Justification = "Perform lifecycle or preparation operations sequentially with cancellation and failure handling at each item; retain explicit ordered awaits.")]
    public async Task RunOnceAsync(CancellationToken token)
    {
        var pending = await (dispatch.PrepareAsync(CapacityExpiryModel.Create, token)).ConfigureAwait(false);
        foreach (var item in pending)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (await (dispatch.ReconcileAsync(item.Request, token)).ConfigureAwait(false))
                    continue;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                await (api.ChangeAsync(item.Request, deadline.Token)).ConfigureAwait(false);
                await (dispatch.ReconcileAsync(item.Request, token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                global::TomasAI.IFM.Domain.Portfolio.ActorLifecycleDiagnosticLog.Write5(logger, error, nameof(CapacityExpiryService), nameof(RunOnceAsync), item.Request.OperationId);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await (RunOnceAsync(stoppingToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                global::TomasAI.IFM.Domain.Portfolio.ActorLifecycleDiagnosticLog.Write4(logger, error, nameof(CapacityExpiryService), nameof(ExecuteAsync));
            }

            try
            {
                await (Task.Delay(TimeSpan.FromSeconds(2), stoppingToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
