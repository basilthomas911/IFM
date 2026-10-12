using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed class NullDatabentoWatchdogPublisher : IDatabentoWatchdogPublisher
{
    public ValueTask PublishAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
