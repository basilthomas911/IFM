using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Trade.ViewModels
{
    public class TradeEndOfDayParameter
    {
        public int FundId { get; set; }
        public int OrderId { get; set; }
        public int TradeId { get; set; }
        public TradeType TradeType { get; set; }
        public string BaseContractId { get; set; }
        public DateOnly ValueDate { get; set; }
    }
}
