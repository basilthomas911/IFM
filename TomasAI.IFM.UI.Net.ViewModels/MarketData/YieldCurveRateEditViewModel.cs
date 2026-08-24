using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Exposes observable duplicate-date validation state for the yield-curve rate dialog.
/// </summary>
public sealed class YieldCurveRateEditViewModel : ObservableObject
{
    readonly IAppRoot _appRoot;
    DateOnly _valueDate;
    bool _rateExists;
    bool _canSave = true;

    /// <summary>Creates the dialog ViewModel from the application composition root.</summary>
    public YieldCurveRateEditViewModel(IAppRoot appRoot)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        CheckValueDateOperation = new AsyncOperation(
            CheckValueDateCoreAsync,
            () => ValueDate > DateOnly.MinValue);
    }

    /// <summary>Gets the selected value date.</summary>
    public DateOnly ValueDate
    {
        get => _valueDate;
        private set => SetProperty(ref _valueDate, value);
    }

    /// <summary>Gets whether a stored curve already uses the selected value date.</summary>
    public bool RateExists
    {
        get => _rateExists;
        private set => SetProperty(ref _rateExists, value);
    }

    /// <summary>Gets whether the dialog may save a new rate for the selected date.</summary>
    public bool CanSave
    {
        get => _canSave;
        private set => SetProperty(ref _canSave, value);
    }

    /// <summary>Gets the guarded duplicate-date validation operation.</summary>
    public IAsyncOperation CheckValueDateOperation { get; }

    /// <summary>Sets the date to validate and resets prior validation state.</summary>
    public void SetValueDate(DateOnly valueDate)
    {
        ValueDate = valueDate;
        RateExists = false;
        CanSave = false;
        CheckValueDateOperation.NotifyCanExecuteChanged();
    }

    async Task CheckValueDateCoreAsync(CancellationToken cancellationToken)
    {
        var rateExists = false;
        await _appRoot.Services.MarketDataQueries.ExecuteObservableAsync(
            async model => rateExists = await model.YieldCurveRateExistsValueAsync(ValueDate),
            cancellationToken);
        RateExists = rateExists;
        CanSave = !RateExists;
    }
}
