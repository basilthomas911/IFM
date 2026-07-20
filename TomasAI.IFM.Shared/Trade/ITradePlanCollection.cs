using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade
{
    public interface ITradePlanCollection : IEnumerable<ITradePlan>
    {
        int Count { get; }

        bool Exists(int orderId);
        void Add(ITradePlan tradePlan);
        void AddRange(ICollection<ITradePlan> tradePlans);
        double AvgTradePnlPercentage(DateOnly valueDate);

        TradePlanReadModel[] ToViewModel();
    }
}
