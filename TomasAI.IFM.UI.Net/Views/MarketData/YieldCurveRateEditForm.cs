using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Models;
using TomasAI.IFM.Contracts;

namespace TomasAI.IFM.Views.MarketData;

/// <summary>
/// Represents a form for editing or adding yield curve rate data.
/// </summary>
/// <remarks>This form allows users to input and validate yield curve rate data for various time periods. It
/// supports both adding new yield curve rates and modifying existing ones. The form ensures that the data entered is
/// valid and provides feedback for invalid inputs. Additionally, it prevents duplicate entries for the same value
/// date.</remarks>
public partial class YieldCurveRateEditForm 
    : Form, IForm<YieldCurveRateEditForm>, IFormControl
{
    IAppRoot? _appRoot = default!;
    YieldCurveRateReadModel? _yieldCurveRate = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="YieldCurveRateEditForm"/> class.
    /// </summary>
    /// <param name="appRoot">The application root object that provides access to shared application services and resources. Cannot be null.</param>
    public YieldCurveRateEditForm(IAppRoot appRoot)
    {
        InitializeComponent();
        _appRoot = appRoot;
    }

    /// <summary>
    /// Gets the view model representing the yield curve rate.
    /// </summary>
    public YieldCurveRateReadModel YieldCurveRate
        => _yieldCurveRate!;

    /// <summary>
    /// Sets the yield curve rate for the current context.
    /// </summary>
    /// <param name="yieldCurveRate">The <see cref="YieldCurveRateReadModel"/> instance representing the yield curve rate to be set.  This parameter
    /// cannot be <see langword="null"/>.</param>
    public void SetYieldCurveRate(YieldCurveRateReadModel yieldCurveRate) 
        => _yieldCurveRate = yieldCurveRate;
    
    /// <summary>
    /// Handles the Load event of the YieldCurveRateEditForm.
    /// </summary>
    /// <remarks>This method is invoked when the form is loaded. It initializes the display of yield curve
    /// rate data by calling the <c>ShowYieldCurveRate</c> method.</remarks>
    /// <param name="sender">The source of the event, typically the form itself.</param>
    /// <param name="e">An <see cref="EventArgs"/> instance containing the event data.</param>
    void YieldCurveRateEditForm_Load(object sender, EventArgs e)
    {
        ShowYieldCurveRate();
    }

    /// <summary>
    /// Displays the yield curve rate details in the user interface, allowing the user to either add a new rate or
    /// modify an existing one.
    /// </summary>
    /// <remarks>If no yield curve rate is set or the value date is uninitialized, the method prepares the
    /// interface for adding a new yield curve rate. Otherwise, it populates the interface with the existing yield curve
    /// rate details and disables editing of the value date.</remarks>
    void ShowYieldCurveRate()
    {
        if (_yieldCurveRate == null|| _yieldCurveRate.ValueDate == DateOnly.MinValue)
        {
            this.Text = "Add Yield Curve Rate";
            dtmValueDate.Value = DateTime.Now;
        }
        else
        {
            this.Text = "Change Yield Curve Rate";
            dtmValueDate.Value = _yieldCurveRate.ValueDate.ToDateTime(TimeOnly.MinValue);
            dtmValueDate.Enabled = false;
            txtOneMonth.Text = $"{_yieldCurveRate.OneMonth:F2}";
            txtTwoMonth.Text = $"{_yieldCurveRate.TwoMonth:F2}";
            txtThreeMonth.Text = $"{_yieldCurveRate.ThreeMonth:F2}";
            txtSixMonth.Text = $"{_yieldCurveRate.SixMonth:F2}";
            txtOneYear.Text = $"{_yieldCurveRate.OneYear:F2}";
            txtTwoYear.Text = $"{_yieldCurveRate.TwoYear:F2}";
            txtThreeYear.Text = $"{_yieldCurveRate.ThreeYear:F2}";
            txtFiveYear.Text = $"{_yieldCurveRate.FiveYear:F2}";
            txtSevenYear.Text = $"{_yieldCurveRate.SevenYear:F2}";
            txtTenYear.Text = $"{_yieldCurveRate.TenYear:F2}";
            txtTwentyYear.Text = $"{_yieldCurveRate.TwentyYear:F2}";
            txtThirtyYear.Text = $"{_yieldCurveRate.ThirtyYear:F2}";
        }
    }

    /// <summary>
    /// Validates the input rates for various time periods and prepares the yield curve rate model if all validations
    /// succeed.
    /// </summary>
    /// <remarks>This method validates the rates for multiple time periods (e.g., 1 month, 2 months, 1 year,
    /// etc.) using the <c>ValidateRate</c> method. If any rate is invalid, the method returns <see langword="false"/>
    /// and does not create the yield curve rate model. If all rates are valid, the method initializes a
    /// <c>YieldCurveRateReadModel</c> instance with the validated rates and returns <see langword="true"/>.</remarks>
    /// <returns><see langword="true"/> if all input rates are valid and the yield curve rate model is successfully created;
    /// otherwise, <see langword="false"/>.</returns>
    bool ValidateOnSave()
    {
        if (!ValidateRate(txtOneMonth.Text, out var oneMonth, "Invalid 1 Month rate") ||
            !ValidateRate(txtTwoMonth.Text, out var twoMonth, "Invalid 2 Month rate") ||
            !ValidateRate(txtThreeMonth.Text, out var threeMonth, "Invalid 3 Month rate") ||
            !ValidateRate(txtSixMonth.Text, out var sixMonth, "Invalid 6 Month rate") ||
            !ValidateRate(txtOneYear.Text, out var oneYear, "Invalid 1 Year rate") ||
            !ValidateRate(txtTwoYear.Text, out var twoYear, "Invalid 2 Year rate") ||
            !ValidateRate(txtThreeYear.Text, out var threeYear, "Invalid 3 Year rate") ||
            !ValidateRate(txtFiveYear.Text, out var fiveYear, "Invalid 5 Year rate") ||
            !ValidateRate(txtSevenYear.Text, out var sevenYear, "Invalid 7 Year rate") ||
            !ValidateRate(txtTenYear.Text, out var tenYear, "Invalid 10 Year rate") ||
            !ValidateRate(txtTwentyYear.Text, out var twentyYear, "Invalid 20 Year rate") ||
            !ValidateRate(txtThirtyYear.Text, out var thirtyYear, "Invalid 30 Year rate"))
            return false;
        _yieldCurveRate = new YieldCurveRateReadModel (
            valueDate: new DateOnly(dtmValueDate.Value.Year, dtmValueDate.Value.Month, dtmValueDate.Value.Day),
            oneMonth: oneMonth,
            twoMonth: twoMonth,
            threeMonth: threeMonth,
            sixMonth: sixMonth,
            oneYear: oneYear,
            twoYear: twoYear,
            threeYear: threeYear,
            fiveYear: fiveYear,
            sevenYear: sevenYear,
            tenYear: tenYear,
            twentyYear: twentyYear,
            thirtyYear: thirtyYear
        );
        return true;
    }

    /// <summary>
    /// Validates whether the specified string can be parsed as a double and provides the parsed value.
    /// </summary>
    /// <param name="value">The string to validate and parse as a double.</param>
    /// <param name="rate">When this method returns, contains the parsed double value if the validation succeeds; otherwise, 0.</param>
    /// <param name="errorMessage">The error message to display in a message box if the validation fails.</param>
    /// <returns><see langword="true"/> if the string is successfully parsed as a double; otherwise, <see langword="false"/>.</returns>
    bool ValidateRate(string value, out double rate, string errorMessage)
    {
        if (!double.TryParse(value, out rate))
        {
            MessageBox.Show(errorMessage, "Validate Year Curve Rates", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Opens the resource or connection associated with this instance.
    /// </summary>
    /// <remarks>This method is intended to initialize and make the resource or connection ready for use. 
    /// Ensure that the resource is properly closed or disposed after use to avoid resource leaks.</remarks>
    /// <exception cref="NotImplementedException">This method is not yet implemented.</exception>
    public void Open()
    {
        throw new NotImplementedException();
    }

    void dtmValueDate_ValueChanged(object sender, EventArgs e)
    {
        var valueDate = new DateOnly(dtmValueDate.Value.Year, dtmValueDate.Value.Month, dtmValueDate.Value.Day);
        if (_yieldCurveRate == null)
        {
            _appRoot?.GetModel<MarketDataQueryModel>().Execute(async ctlr => {
                ctlr.OnError((errorCode, errorMsg) => this.Post(() => MessageBox.Show(text: errorMsg, caption: "Yield Curve Rate Error", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Error)));
                await ctlr.YieldCurveRateExistsAsync(valueDate, serviceResult =>
                    this.Post(() => {
                        btnSave.Enabled = false;
                        if (!serviceResult.Success)
                            MessageBox.Show($"Yield Curve Rate data error : {serviceResult.ErrorMessage}", "Validate Year Curve Rates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else if (serviceResult.Value!.Value)
                            MessageBox.Show($"Yield Curve Rate already exists for : {valueDate:yyyy-MMM-dd}", "Validate Year Curve Rates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else
                            btnSave.Enabled = true;
                    }));
            });
        }
    }


    void btnSave_Click(object sender, EventArgs e)
    {
        if (ValidateOnSave())
            this.Close();
    }

    void btnCancel_Click(object sender, EventArgs e)
    {
        _yieldCurveRate = null;
        this.Close();
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }
   
}

