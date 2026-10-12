using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
        public string[] OptionLegContractIds { get; set; }
    }
}
