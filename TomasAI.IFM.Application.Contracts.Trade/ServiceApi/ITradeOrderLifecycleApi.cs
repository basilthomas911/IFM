using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

/// <summary>Starts the canonical actor lifecycle for a Trade Order already accepted by Portfolio.</summary>
public interface ITradeOrderLifecycleApi
{
    /// <summary>Creates, approves, marks ready, and binds one accepted order to an execution attempt.</summary>
    /// <param name="order">The immutable order returned by Portfolio acceptance.</param>
    /// <param name="portfolioEventId">The Portfolio completion event that authorized the order.</param>
    /// <param name="channel">The selected execution channel.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The final bind-command result.</returns>
    Task<ServiceResult<Guid>> SubmitAcceptedAsync(
        TradeOrderDefinition order,
        Guid portfolioEventId,
        ExecutionChannel channel,
        CancellationToken cancellationToken = default);
}
