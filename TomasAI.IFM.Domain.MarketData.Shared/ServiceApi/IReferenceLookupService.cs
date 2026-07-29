using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

public interface IReferenceLookupService
{
    bool CurrencyExists(string shortCode);
    bool ExchangeExists(string shortCode);
    bool MultiplierExists(string shortCode);
    bool SecurityTypeExists(string shortCode);
    bool SymbolExists(string shortCode);
}
