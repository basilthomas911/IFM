using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

public record FuturesEodDataIndexReadModel(
    DateOnly ValueDate,
    string ContractId )
{
}
