using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared
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
