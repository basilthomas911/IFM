using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeFill
{
    int OrderId { get; }
    int TradeId { get; }
    DateTime FillDate { get; }
    int FillQuantity { get; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    TradeFillReadModel ToViewModel();
}
