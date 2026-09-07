using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Records or resubmits an original immutable outcome, without acquiring provider data.</summary>
public sealed class DownloadLogCommandApi(IActorProducer producer) : TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IDownloadLogCommandApi
{
    public ValueTask<ServiceResult<GuidResult>> RecordAsync(MarketDataDownloadOutcome outcome, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var command = new InsertMarketDataDownloadLogCommand(outcome);
        return producer.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId, GuidResult>(command.Subject, command, command.EntityId, cancellationToken);
    }
}
