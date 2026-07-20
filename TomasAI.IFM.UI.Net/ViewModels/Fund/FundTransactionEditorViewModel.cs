using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;

public class FundTransactionEditorViewModel
{
    readonly IAppRoot _appRoot;

    List<FundReadModel> _funds = null!;
    List<FundTransactionReadModel> _fundTransactions = null!;
    decimal _fundBalance;

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
    public Action<string, string> OnErrorMessage { get; set; } = null!;
    public Action<ICollection<FundReadModel>> OnFundsLoaded { get; set; } = null!;
    public Action<ICollection<FundTransactionReadModel>> OnFundTransactionsLoaded { get; set; } = null!;
    public Action<string> OnTransactionCommentLoaded { get; set; } = null!;
    public Action<decimal> OnFundBalanceLoaded { get; set; } = null!;
    public Action<FundPnlReportReadModel> OnFundPnlReportLoaded { get; set; } = null!;

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
            await model.GetFundTransactionsAsync(fundId, DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate), funds => {
                _fundTransactions = [.. funds];
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
            await model.GetFundPnlReportAsync(fundId, DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate.AddDays(1)), fundPnlReport => {
                if (fundPnlReport != null) 
                    OnFundPnlReportLoaded(fundPnlReport);
            });
        });

    /// <summary>
    /// return transaction comment
    /// </summary>
    /// <param name="transactionId"></param>
    public void LoadTransactionComment(FundTransactionId? transactionId)
        => OnTransactionCommentLoaded(_fundTransactions == null ? string.Empty : _fundTransactions.Where(e => e.Id == transactionId).SingleOrDefault()?.Description ?? string.Empty);

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
    public FundTransactionId? GetFundTransactionId(int index) => _fundTransactions == null || _fundTransactions.Count == 0 ? default : _fundTransactions[index].Id;

    /// <summary>
    /// return fund transaction
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    public FundTransactionReadModel? GetFundTransaction(int index) => _fundTransactions == null || _fundTransactions.Count == 0 ? null : _fundTransactions[index];

}
