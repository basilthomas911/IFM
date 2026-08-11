using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Hosts the trade-order confirmation workflow in a WinForms modal dialog.</summary>
public sealed class WinFormsTradeOrderConfirmationService(IWin32Window? owner = null)
    : ITradeOrderConfirmationService
{
    /// <inheritdoc />
    public ValueTask<TradeOrderConfirmationResult> ConfirmAsync(
        TradeOrderReadModel tradeOrder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var viewModel = new TradeOrderConfirmationViewModel(tradeOrder);
        using var dialog = new TradeOrderConfirmationForm(viewModel);
        var dialogResult = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return ValueTask.FromResult(new TradeOrderConfirmationResult(
            dialogResult == DialogResult.OK && viewModel.CanConfirm,
            viewModel.SelectedTradeFillType));
    }
}
