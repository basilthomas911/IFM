using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record GetFuturesOptionSpreadRequest(
        Guid CommandId,
        FuturesOptionContractReadModel QueryForShortContract,
        FuturesOptionContractReadModel QueryForLongContract)
    {
    }

    public record GetFuturesOptionSpreadResponse(
        Guid CommandId,
        FuturesOptionContractReadModel? ShortOptionContract,
        FuturesOptionContractReadModel? LongOptionContract)
    {
    }
}
