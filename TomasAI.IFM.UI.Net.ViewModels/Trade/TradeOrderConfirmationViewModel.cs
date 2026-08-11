using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Exposes framework-neutral trade-order confirmation state.</summary>
public sealed class TradeOrderConfirmationViewModel : ObservableObject
{
    static readonly IReadOnlyList<TradeFillType> AvailableFillTypes =
        [TradeFillType.Manual, TradeFillType.Broker];
    TradeFillType _selectedTradeFillType = TradeFillType.Manual;

    /// <summary>Creates confirmation state for a fully calculated trade order.</summary>
    public TradeOrderConfirmationViewModel(TradeOrderReadModel tradeOrder)
        => TradeOrder = tradeOrder ?? throw new ArgumentNullException(nameof(tradeOrder));

    /// <summary>Gets the order presented for confirmation.</summary>
    public TradeOrderReadModel TradeOrder { get; }

    /// <summary>Gets supported fill-source choices.</summary>
    public IReadOnlyList<TradeFillType> TradeFillTypes => AvailableFillTypes;

    /// <summary>Gets the selected fill source.</summary>
    public TradeFillType SelectedTradeFillType
    {
        get => _selectedTradeFillType;
        private set
        {
            if (!SetProperty(ref _selectedTradeFillType, value))
                return;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    /// <summary>Gets whether the selected fill source is currently implemented.</summary>
    public bool CanConfirm => SelectedTradeFillType == TradeFillType.Manual;

    /// <summary>Selects a fill source by safe list index.</summary>
    public bool SelectTradeFillType(int index)
    {
        if (index < 0 || index >= TradeFillTypes.Count)
            return false;
        var selection = TradeFillTypes[index];
        if (selection == SelectedTradeFillType)
            return false;
        SelectedTradeFillType = selection;
        return true;
    }
}
