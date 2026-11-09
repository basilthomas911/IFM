using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

public class OptionTradeSpreadBarUIViewModel
{
    readonly OptionTradeSpreadBarsDataModel _optionTradeSpreadBarData;


    public OptionTradeSpreadBarUIViewModel(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData)
    {
        _optionTradeSpreadBarData = optionTradeSpreadBarData;

    }
    public DateTime BarDate => EasternTime.FromUtc(_optionTradeSpreadBarData.BarDate);
    public decimal LossLimit => _optionTradeSpreadBarData.LossLimit;
    public decimal WinLimit => _optionTradeSpreadBarData.WinLimit;
    public decimal ForwardSpread => _optionTradeSpreadBarData.ForwardSpread;
    public decimal NetSpread => _optionTradeSpreadBarData.NetSpread;


}
