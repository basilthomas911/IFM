using System;
using TomasAI.IFM.Shared.MarketData.Commands;

namespace TomasAI.IFM.Application.Command.RestApi.Services
{
    public interface IMarketDataCommandService
    {
        void Execute(AddFuturesContractCommand e);
        void Execute(ChangeFuturesContractCommand e);
        void Execute(RemoveFuturesContractCommand e);
    }
}
