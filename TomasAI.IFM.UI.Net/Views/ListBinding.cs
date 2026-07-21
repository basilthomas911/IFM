namespace TomasAI.IFM.UI.Net.Views;

/// <summary>
/// Represents a binding between a data source and a display control, allowing for data display and interaction.
/// </summary>
/// <remarks>This class facilitates the binding of a data source to a display control, such as a list box or combo
/// box,  by setting the control's data source and display member. It provides methods for initializing the display
/// state  and retrieving the value of the currently selected item.</remarks>
/// <typeparam name="TData">The type of the data source being bound to the display control.</typeparam>
/// <param name="displayMember"></param>
/// <param name="displayControl"></param>
/// <param name="dataSource"></param>
/// <param name="getValue"></param>
public class ListBinding<TData>(string displayMember, Control displayControl,TData dataSource, Func<int, object> getValue = null!)
{
    /// <summary>
    /// Loads the data source into the display control and sets the initial display state.
    /// </summary>
    /// <remarks>This method initializes the display control by setting its data source and display member, 
    /// and ensures the first item is selected. If a <paramref name="loadComplete"/> callback is provided,  it will be
    /// executed after the loading process completes.</remarks>
    /// <param name="loadComplete">An optional callback to invoke after the data source has been loaded and the display state is initialized.</param>
    /// <returns>The current instance of <see cref="ListBinding{TData}"/>, allowing for method chaining.</returns>
    public ListBinding<TData> Load(Action loadComplete = null!)
    {
        displayControl?.InvokeAsync( () =>
        {
            dynamic listControl = displayControl;
            listControl.DisplayMember = displayMember;
            listControl.DataSource = dataSource;
            listControl.SelectedIndex = 0;
            loadComplete?.Invoke();
        });
        return this;
    }

    /// <summary>
    /// Retrieves the value of the currently selected item in the list control, optionally formatted as a string.
    /// </summary>
    /// <param name="format">An optional format string that specifies how the value should be formatted.  If <see langword="null"/>, the
    /// value is returned as a string without additional formatting.</param>
    /// <returns>A string representation of the selected item's value. If <paramref name="format"/> is provided,  the value is
    /// formatted according to the specified format string.</returns>
    public string GetValue(string format=null!)
    {
        dynamic listControl = displayControl;
        var listIndex = listControl.SelectedIndex;
        return format is null 
            ? $"{getValue(listIndex)}"
            : getValue(listIndex).ToString(format);
    }
}
