using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Reports;

namespace TomasAI.IFM.Domain.Trade.Shared.Contracts
{
    public interface IRibbonEventHandler
    {
        void OpenTradeBlotter(TradeType tradeBlotterType);
        void OpenInvestorManager();
        void OpenMarketDataManager();
        void RunReport(ReportTypeEnum reportType);
        void LoadTrades();
        void RunTrades();
    }
}
