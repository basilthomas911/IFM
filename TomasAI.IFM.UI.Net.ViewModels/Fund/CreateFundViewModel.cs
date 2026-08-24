using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
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
    readonly IReferenceDataService _referenceDataService;
    FundReadModel? _pendingFund;
    FundReadModel? _createdFund;
    int _newFundId;

    /// <summary>Creates the workflow with its application root and explicit Reference service.</summary>
    public CreateFundReadModel(IAppRoot appRoot, IReferenceDataService referenceDataService)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _referenceDataService = referenceDataService
            ?? throw new ArgumentNullException(nameof(referenceDataService));
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

    async Task LoadNewFundIdCoreAsync(CancellationToken cancellationToken)
        => NewFundId = (await _referenceDataService.GetNextFundIdAsync(cancellationToken)).RequireValue();

    Task CreateFundCoreAsync(CancellationToken cancellationToken)
        => _appRoot.Services.FundCommands.ExecuteObservableAsync(
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
