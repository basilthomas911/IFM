using System.Runtime.CompilerServices;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>Applies the system-wide Eastern-time display policy to WinForms controls.</summary>
internal static class EasternTimeDisplayPolicy
{
    static readonly ConditionalWeakTable<Control, object> ConfiguredControls = new();

    public static void ApplyEasternTimeDisplayPolicy(this Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Attach(root);
    }

    static void Attach(Control control)
    {
        if (ConfiguredControls.TryGetValue(control, out _))
            return;

        ConfiguredControls.Add(control, new object());
        control.ControlAdded += ControlAdded;
        if (control is DataGridView grid)
            grid.CellFormatting += FormatBackendTimestamp;

        foreach (Control child in control.Controls)
            Attach(child);
    }

    static void ControlAdded(object? sender, ControlEventArgs eventArgs)
        => Attach(eventArgs.Control);

    static void FormatBackendTimestamp(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        eventArgs.Value = eventArgs.Value switch
        {
            DateTime value => EasternTime.FromUtc(value),
            DateTimeOffset value => EasternTime.FromUtc(value),
            _ => eventArgs.Value
        };
    }
}
