using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.UI.Net.ViewModels.Presentation;

/// <summary>
/// Provides framework-neutral property-change notification for shared presentation state.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Updates a property backing field and raises <see cref="PropertyChanged"/> when its value changes.
    /// </summary>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises a property-change notification for a computed or externally updated property.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
