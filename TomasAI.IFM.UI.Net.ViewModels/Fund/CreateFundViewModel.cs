using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;

/// <summary>
/// Exposes observable state and guarded operations for creating a fund.
/// </summary>
public sealed class CreateFundReadModel : ObservableObject
{
    readonly IAppRoot _appRoot;
    FundReadModel? _pendingFund;
    FundReadModel? _createdFund;
    int _newFundId;

    public CreateFundReadModel(IAppRoot appRoot)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        LoadNewFundIdOperation = new AsyncOperation(LoadNewFundIdCoreAsync);
        CreateFundOperation = new AsyncOperation(
            CreateFundCoreAsync,
            () => _pendingFund?.IsValid == true);
    }

    public int NewFundId
    {
        get => _newFundId;
        private set => SetProperty(ref _newFundId, value);
    }

    public FundReadModel? CreatedFund
    {
        get => _createdFund;
        private set => SetProperty(ref _createdFund, value);
    }

    public IAsyncOperation LoadNewFundIdOperation { get; }
    public IAsyncOperation CreateFundOperation { get; }

    public void SetPendingFund(FundReadModel fund)
    {
        _pendingFund = fund ?? throw new ArgumentNullException(nameof(fund));
        CreateFundOperation.NotifyCanExecuteChanged();
    }

    Task LoadNewFundIdCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<ReferenceQueryModel>().ExecuteObservableAsync(
            async model =>
            {
                var fundId = 0;
                await model.NewFundIdAsync(loaded => fundId = loaded);
                NewFundId = fundId;
            },
            cancellationToken);

    Task CreateFundCoreAsync(CancellationToken cancellationToken)
        => _appRoot.GetModel<FundCommandModel>().ExecuteObservableAsync(
            async model =>
            {
                var fund = _pendingFund
                    ?? throw new InvalidOperationException("A valid pending fund is required.");
                var completed = false;
                await model.CreateFundAsync(fund, () => completed = true);
                if (completed)
                    CreatedFund = fund;
            },
            cancellationToken);
}
