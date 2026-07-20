using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.TradePlan.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradePlanDb
{
    public interface ITradePlanDbWriteContext : ITradePlanDbContext
    {
        Task InsertIronCondorTradePlanAsync(IronCondorTradePlanReadModel tradePlan);
    }
}
