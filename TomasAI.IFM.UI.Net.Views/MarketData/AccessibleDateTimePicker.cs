using System.Globalization;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

/// <summary>
/// Exposes a writable UI Automation value for the WinForms date picker.
/// </summary>
/// <remarks>
/// The stock WinForms provider advertises a Value pattern but does not apply values written through it.
/// This provider keeps the standard picker UI while making date entry available to assistive technology.
/// </remarks>
class AccessibleDateTimePicker : DateTimePicker
{
    protected override AccessibleObject CreateAccessibilityInstance()
        => new DateTimePickerAccessibleObject(this);

    sealed class DateTimePickerAccessibleObject(AccessibleDateTimePicker owner)
        : ControlAccessibleObject(owner)
    {
        public override string? Name
        {
            get => $"Date, {owner.Value:D}";
            set => base.Name = value;
        }

        public override string? Value
        {
            get => owner.Value.ToString("D", CultureInfo.CurrentCulture);
            set
            {
                if (!DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
                    && !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                    throw new ArgumentException($"'{value}' is not a valid date.", nameof(value));

                void Apply() => owner.Value = parsed.Date;
                if (owner.InvokeRequired)
                    owner.Invoke(Apply);
                else
                    Apply();
            }
        }
    }
}
