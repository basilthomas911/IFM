using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Application.Storage.FundDb
{
    public interface IFundDbWriteContext : IFundDbContext
    {
        Task DeleteFundOrderAsync(FundOrderId fundOrderId, DateTime removedDate, string removedBy);
        Task DeleteFundOrderTradeAsync(FundOrderTradeId fundOrderTradeId);
        Task InsertFundAsync(FundReadModel fund);
        Task InsertFundOrderAsync(FundOrderReadModel fundOrder);
        Task InsertFundOrderTradeAsync(FundOrderTradeReadModel fundOrderTrade);
        Task InsertFundTransactionAsync(FundTransactionReadModel fundTransaction);
        Task InsertFundTransactionsAsync(ICollection<FundTransactionReadModel> fundTransactions);
        Task UpdateFundOrderStatusAsync(FundOrderId fundOrderId, Shared.Fund.OrderStatus orderStatus);
        Task UpdateFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, DateTime updatedOn, string updatedBy);
        new Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);

    }
}
