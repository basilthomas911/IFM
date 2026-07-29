using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels
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
