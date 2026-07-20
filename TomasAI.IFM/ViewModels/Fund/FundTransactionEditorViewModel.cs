using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Fund.ViewModels;

namespace TomasAI.IFM.ViewModels.Fund
{
    public class FundTransactionEditorViewModel
    {
        private readonly IAppRoot _appRoot;
        private List<FundReadModel> _funds;
        private List<FundTransactionReadModel> _fundTransactions;
        private decimal _fundBalance;

        /// <summary>
        /// create fund transaction view model
        /// </summary>
        /// <param name="appRoot"></param>
        public FundTransactionEditorViewModel(IAppRoot appRoot)
        {
            _appRoot = appRoot;
        }

        public IAppRoot AppRoot => _appRoot;
        public decimal FundBalance => _fundBalance; 
        public Action<string, string> OnErrorMessage { get; set; }
        public Action<ICollection<FundReadModel>> OnFundsLoaded { get; set; }
        public Action<ICollection<FundTransactionReadModel>> OnFundTransactionsLoaded { get; set; }
        public Action<string> OnTransactionCommentLoaded { get; set; }
        public Action<decimal> OnFundBalanceLoaded { get; set; }
        public Action<FundPnlReportReadModel> OnFundPnlReportLoaded { get; set; }

        /// <summary>
        /// load funds from storage
        /// </summary>
        /// <param name="selectedIndex"></param>
        public void LoadFunds()
            => _appRoot.GetModel<FundQueryModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnErrorMessage(errorMsg, "Loading Funds Error"));
                await model.GetFundsAsync(funds =>
                {
                    if (funds?.Length > 0)
                    {
                       _funds = new List<FundReadModel>(funds);
                       OnFundsLoaded(funds);
                    }
                });
            });

        /// <summary>
        /// return list of transactions for selected fund by date range
        /// </summary>
        /// <param name="fundId">selected fund</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public void LoadFundTransactions(int fundId, DateTime startDate, DateTime endDate)
            => _appRoot.GetModel<FundQueryModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnErrorMessage(errorMsg, "Loading Fund Transactions Error"));
                await model.GetFundTransactionsAsync(fundId, startDate, endDate, funds => {
                    _fundTransactions = new List<FundTransactionReadModel>(funds);
                    OnFundTransactionsLoaded(_fundTransactions);
                    LoadFundBalance(fundId);
                });
            });

        /// <summary>
        /// return fund balance
        /// </summary>
        /// <param name="fundId"></param>
        public void LoadFundBalance(int fundId)
            => _appRoot.GetModel<FundQueryModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnErrorMessage(errorMsg, "Loading Fund Balance Error"));
                await model.GetFundBalanceAsync(fundId, fundBalance => {
                    _fundBalance = fundBalance;
                    OnFundBalanceLoaded(fundBalance);
                });
            });

        /// <summary>
        /// return list of transactions for selected fund by date range
        /// </summary>
        /// <param name="fundId">selected fund</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public void LoadFundPnlReport(int fundId, DateTime startDate, DateTime endDate)
            => _appRoot.GetModel<FundQueryModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => OnErrorMessage(errorMsg, "Loading Fund Pnl Report Error"));
                await model.GetFundPnlReportAsync(fundId, startDate, endDate.Date.AddDays(1).AddMilliseconds(-1), fundPnlReport => {
                    if (fundPnlReport != null) 
                        OnFundPnlReportLoaded(fundPnlReport);
                });
            });

        /// <summary>
        /// return transaction comment
        /// </summary>
        /// <param name="transactionId"></param>
        public void LoadTransactionComment(int transactionId)
            => OnTransactionCommentLoaded(_fundTransactions == null ? string.Empty : _fundTransactions.Where(e => e.TransactionId == transactionId).SingleOrDefault()?.Description ?? string.Empty);

        /// <summary>
        /// return fund id from  drop down list selected index
        /// </summary>
        /// <param name="index">drop down list selected index</param>
        /// <returns></returns>
        public int GetFundId(int index) => _funds == null || _funds.Count == 0 ? -1 : _funds[index].FundId;

        /// <summary>
        /// return fund transaction id from selected transaction grid
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public int GetFundTransactionId(int index) => _fundTransactions == null || _fundTransactions.Count == 0 ? -1 : _fundTransactions[index].TransactionId;

        /// <summary>
        /// return fund transaction
        /// </summary>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        public FundTransactionReadModel GetFundTransaction(int index) => _fundTransactions == null || _fundTransactions.Count == 0 ? null : _fundTransactions[index];

    }
}
