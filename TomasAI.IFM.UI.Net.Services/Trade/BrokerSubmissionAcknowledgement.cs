using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>A definite broker rejection, distinct from an unconfirmed dispatch or timeout.</summary>
public sealed class BrokerOrderRejectedException(string message) : Exception(message);

/// <summary>Observes durable broker state for the exact submitted orders; dispatch alone is not acceptance.</summary>
public static class BrokerSubmissionAcknowledgement
{
    public static async Task WaitAsync(IBrokerOrderQueryApi queries, IReadOnlyList<TradeOrderDefinition> orders,
        CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                var accepted = orders.Count > 0;
                foreach (var order in orders)
                {
                    var result = await queries.ListAsync(order.Id, deadline.Token).ConfigureAwait(false);
                    if (!result.Success || result.Value is null)
                        throw new InvalidOperationException("Broker acknowledgement unavailable: " + result.ErrorMessage);
                    foreach (var broker in result.Value)
                        if (broker.Status is BrokerOrderStatus.Rejected or BrokerOrderStatus.Cancelled)
                            throw new BrokerOrderRejectedException(string.Join(": ", new[] {
                                broker.LastObservation?.Category ?? broker.DispatchCategory,
                                broker.LastObservation?.Detail ?? broker.DispatchDetail }.Where(x => !string.IsNullOrWhiteSpace(x))));
                    accepted &= order.Components.Length > 0 && order.Components.All(component => result.Value.Any(broker =>
                        broker.Id.Execution.TradeOrder == order.Id && broker.Id.ComponentId == component.ComponentId &&
                        broker.Status is BrokerOrderStatus.Working or BrokerOrderStatus.PartiallyFilled or BrokerOrderStatus.Filled));
                }
                if (accepted) return;
                await Task.Delay(200, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Broker acceptance is unconfirmed; reconcile this order before retrying.", error);
        }
    }
}
