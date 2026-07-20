using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.ScheduledTask.SetClosingPrice.UnitTests
{
    public  class SampleData
    {
        public static FuturesTickDataViewModel FuturesTickData 
            => new FuturesTickDataViewModel(
                ValueDate: new DateTime(2022, 10, 10),
                ContractId: "ES20220318",
                TickDate: new DateTime(2022, 10, 10),
                TickTime: 1000,
                Price: 3554.25,
                Size: 2000);

        public static FuturesContractViewModel FuturesContract
            => new FuturesContractViewModel(
                ContractId: "ES20220318",
                Description: "ES: e-mini S&P 500 futures 2022 Mar 18 @ GLOBEX",
                Symbol: "ES",
                LocalSymbol: "ESH2",
                SecurityType: "FUT",
                Currency: "USD",
                Exchange: "GLOBEX",
                Multiplier: "50",
                LastTradeDate: new DateTime(2022, 03, 18),
                CurrentlyTraded: true);
    }
}
