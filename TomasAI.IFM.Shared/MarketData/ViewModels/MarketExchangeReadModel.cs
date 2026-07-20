using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketData.ViewModels
{
    public record MarketExchangeReadModel(
        string Symbol,
        string Exchange,
        DayOfWeek DayOfWeek,
        string MarketOpen,
        string MarketClose)
    {
    }
}
