using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.ViewModels.Trade;

public class OptionTradeSpreadBarUIViewModel
{
    readonly OptionTradeSpreadBarsDataModel _optionTradeSpreadBarData;
    readonly IronCondorMDILimitDataModel _ironCondorMDILimit;

    public OptionTradeSpreadBarUIViewModel(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData, IronCondorMDILimitDataModel ironCondorMDILimit) 
    {
        _optionTradeSpreadBarData = optionTradeSpreadBarData;
        _ironCondorMDILimit = ironCondorMDILimit;
    }
    public DateTime BarDate => _optionTradeSpreadBarData.BarDate;
    public decimal LossLimit => _optionTradeSpreadBarData.LossLimit;
    public decimal WinLimit => _optionTradeSpreadBarData.WinLimit;
    public decimal ForwardSpread => _optionTradeSpreadBarData.ForwardSpread;
    public decimal NetSpread => _optionTradeSpreadBarData.NetSpread;
    public double MDIWarningLimit => _ironCondorMDILimit?.WarningLimit ?? 0;
    public double MDIMaxLimit => _ironCondorMDILimit?.MaxLimit ?? 0;
}
