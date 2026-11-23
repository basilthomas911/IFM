using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;
public sealed class IronCondorAutomaticExitWorker(IronCondorAutomaticExitDispatcher dispatcher,IActorSupervisor supervisor,
    ILogger<IronCondorAutomaticExitWorker> logger, IronCondorCloseProgress progress):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        try {do {
            if(!supervisor.IsReady)continue;
            try {await dispatcher.RunOneAsync(token).ConfigureAwait(false);await progress.RunAsync(token).ConfigureAwait(false);}
            catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
            catch(Exception error){logger.LogError(error,"Automatic iron-condor exit dispatch remains pending.");}
        } while(await timer.WaitForNextTickAsync(token).ConfigureAwait(false));}
        catch(OperationCanceledException) when(token.IsCancellationRequested){}
    }
}
