using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.ViewModels
{
    public class TradeInfoReadModel
    {
        public int OrderId { get; set; }
        public int TradeId { get; set; }
        public TradeType TradeType { get; set; }
        public int Quantity { get; set; }
        public DateOnly TradeDate { get; set; }
        public DateOnly MaturityDate { get; set; }
        public TradeState TradeState { get; set; }
        public TradeAction TradeAction { get; set; }
        public string[] OptionLegContractIds { get; set; }
    }
}
