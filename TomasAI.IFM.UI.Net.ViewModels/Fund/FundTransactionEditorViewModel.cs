using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;

/// <summary>
/// Exposes observable fund-transaction state and guarded query operations.
/// </summary>
public sealed class FundTransactionEditorViewModel : ObservableObject
{
    readonly IAppRoot _appRoot;
    IReadOnlyList<FundReadModel> _funds = [];
    IReadOnlyList<FundTransactionReadModel> _fundTransactions = [];
    FundPnlReportReadModel? _fundPnlReport;
    string _transactionComment = string.Empty;
    decimal _fundBalance;
    int _selectedFundId = -1;
    DateTime _fromDate;
    DateTime _toDate;

    public FundTransactionEditorViewModel(IAppRoot appRoot)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        LoadFundsOperation = new AsyncOperation(LoadFundsCoreAsync);
        LoadFundDetailsOperation = new AsyncOperation(
            LoadFundDetailsCoreAsync,
            () => _selectedFundId >= 0 && _fromDate <= _toDate);
    }

    public IAppRoot AppRoot => _appRoot;
    public IReadOnlyList<FundReadModel> Funds
    {
        get => _funds;
        private set => SetProperty(ref _funds, value);
    }

    public IReadOnlyList<FundTransactionReadModel> FundTransactions
    {
        get => _fundTransactions;
        private set => SetProperty(ref _fundTransactions, value);
    }

    public decimal FundBalance
    {
        get => _fundBalance;
        private set => SetProperty(ref _fundBalance, value);
    }

    public FundPnlReportReadModel? FundPnlReport
    {
        get => _fundPnlReport;
        private set => SetProperty(ref _fundPnlReport, value);
    }

    public string TransactionComment
    {
        get => _transactionComment;
        private set => SetProperty(ref _transactionComment, value);
    }

    public IAsyncOperation LoadFundsOperation { get; }
    public IAsyncOperation LoadFundDetailsOperation { get; }

    public void SetFundDetailsFilter(int fundId, DateTime fromDate, DateTime toDate)
    {
        _selectedFundId = fundId;
        _fromDate = fromDate;
        _toDate = toDate;
        LoadFundDetailsOperation.NotifyCanExecuteChanged();
    }

    public void SelectTransaction(int index)
        => TransactionComment = GetFundTransaction(index)?.Description ?? string.Empty;

    public int GetFundId(int index)
        => index >= 0 && index < Funds.Count ? Funds[index].FundId : -1;

    public FundTransactionReadModel? GetFundTransaction(int index)
        => index >= 0 && index < FundTransactions.Count ? FundTransactions[index] : null;

    Task LoadFundsCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<FundQueryModel>().ExecuteObservableAsync(
            async model =>
            {
                FundReadModel[] funds = [];
                await model.GetFundsAsync(loaded => funds = loaded ?? []);
                Funds = funds;
            },
            cancellationToken);

    Task LoadFundDetailsCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<FundQueryModel>().ExecuteObservableAsync(
            async model =>
            {
                FundTransactionReadModel[] transactions = [];
                var balance = 0m;
                FundPnlReportReadModel? report = null;
                var fromDate = DateOnly.FromDateTime(_fromDate);
                var toDate = DateOnly.FromDateTime(_toDate);

                await model.GetFundTransactionsAsync(
                    _selectedFundId,
                    fromDate,
                    toDate,
                    loaded => transactions = loaded ?? []);
                await model.GetFundBalanceAsync(_selectedFundId, loaded => balance = loaded);
                await model.GetFundPnlReportAsync(
                    _selectedFundId,
                    fromDate,
                    DateOnly.FromDateTime(_toDate.AddDays(1)),
                    loaded => report = loaded);

                FundTransactions = transactions;
                FundBalance = balance;
                FundPnlReport = report;
                TransactionComment = string.Empty;
            },
            cancellationToken);
}
