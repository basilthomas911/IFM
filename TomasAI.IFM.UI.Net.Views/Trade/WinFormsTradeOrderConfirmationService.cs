using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Holds the modal confirmation until the shared submission finishes receiving broker acceptance.</summary>
public sealed class WinFormsTradeOrderConfirmationService(IWin32Window? owner = null)
    : ITradeOrderSubmissionConfirmationService
{
    readonly SynchronizationContext ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    TradeOrderConfirmationForm? dialog;
    public ValueTask<TradeOrderConfirmationResult> ConfirmAsync(TradeOrderReadModel order, CancellationToken cancellationToken = default)
        => ConfirmAsync(order, [], cancellationToken);

    public ValueTask<TradeOrderConfirmationResult> ConfirmAsync(TradeOrderReadModel order,
        IReadOnlyList<TradeOrderConfirmationLeg> legs, CancellationToken cancellationToken = default)
    {
        var decision = new TaskCompletionSource<TradeOrderConfirmationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        ui.Post(_ => {
            if(cancellationToken.IsCancellationRequested) { decision.TrySetCanceled(cancellationToken); return; }
            var viewModel = new TradeOrderConfirmationViewModel(order, legs);
            using var form = new TradeOrderConfirmationForm(viewModel);
            dialog=form;
            form.ConfirmRequested = () => decision.TrySetResult(new(true, viewModel.SelectedTradeFillType));
            try {
                if(owner is null) form.ShowDialog(); else form.ShowDialog(owner);
                decision.TrySetResult(new(false, viewModel.SelectedTradeFillType));
            } catch(Exception error) { decision.TrySetException(error); }
            finally { dialog=null; }
        }, null);
        return new(decision.Task);
    }

    public ValueTask CompleteSubmissionAsync(string? error)
    {
        var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ui.Post(_ => {
            try { dialog?.CompleteSubmission(); closed.TrySetResult(); }
            catch(Exception failure) { closed.TrySetException(failure); }
        }, null);
        // The owning WinForms command handler presents exceptions after this modal has closed.
        return new(closed.Task);
    }
}
