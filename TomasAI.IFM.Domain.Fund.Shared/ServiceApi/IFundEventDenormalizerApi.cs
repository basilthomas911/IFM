using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Fund.Shared.Events;

namespace TomasAI.IFM.Domain.Fund.Shared.ServiceApi
{
    public interface IFundEventDenormalizerApi
    {
        Task InsertFundAsync(FundCreatedEvent e);
        Task InsertFundOrderAsync(OrderAddedToFundEvent e);
        Task DeleteFundOrderAsync(OrderRemovedFromFundEvent e);
        Task InsertFundOrderTradeAsync(TradeAddedToFundOrderEvent e);
        Task DeleteFundOrderTradeAsync(TradeRemovedFromFundOrderEvent e);
        Task UpdateFundOrderTradeStateAsync(FundOrderTradeStateChangedEvent e);
        Task InsertFundTransactionAsync(OpeningTradeFundTransactionCreatedEvent e);
        Task InsertFundTransactionAsync(OpeningTradeFundTransactionAdjustmentCreatedEvent e);
        Task InsertFundTransactionAsync(TradeCommissionFundTransactionCreatedEvent e);
        Task InsertFundTransactionAsync(TradeCommissionFundTransactionAdjustmentCreatedEvent e);
        Task InsertFundTransactionAsync(RealizedTradePnlFundTransactionCreatedEvent e);
        Task InsertFundTransactionAsync(RealizedTradePnlFundTransactionAdjustmentCreatedEvent e);
        Task InsertFundTransactionAsync(UnrealizedTradePnlFundTransactionCreatedEvent e);
        Task InsertFundTransactionAsync(UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent e);
        Task UpdateFundOrderStatusAsync(FundOrderStatusChangedEvent e);
    }
}
