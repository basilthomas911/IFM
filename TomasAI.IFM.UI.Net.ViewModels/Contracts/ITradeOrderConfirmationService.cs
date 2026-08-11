using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.UI.Net.Contracts;

/// <summary>Requests operator confirmation for a fully calculated trade order.</summary>
public interface ITradeOrderConfirmationService
{
    /// <summary>Displays the host-specific confirmation experience and returns the operator decision.</summary>
    ValueTask<TradeOrderConfirmationResult> ConfirmAsync(
        TradeOrderReadModel tradeOrder,
        CancellationToken cancellationToken = default);
}

/// <summary>Contains the operator decision and selected fill source.</summary>
public readonly record struct TradeOrderConfirmationResult(
    bool IsConfirmed,
    TradeFillType TradeFillType);
