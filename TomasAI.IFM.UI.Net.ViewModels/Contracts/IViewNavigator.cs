namespace TomasAI.IFM.UI.Net.Contracts;

/// <summary>
/// Creates and presents application views without exposing a concrete UI framework to the composition root.
/// </summary>
public interface IViewNavigator
{
    /// <summary>
    /// Resolves a view for hosting by the application shell.
    /// </summary>
    TView CreateView<TView>() where TView : class;

    /// <summary>
    /// Configures and displays a modal view, returning a framework-neutral result.
    /// </summary>
    NavigationResult ShowModal<TView>(Action<TView>? initialize = null) where TView : class;
}

public enum NavigationResult
{
    None,
    Accepted,
    Cancelled,
    Yes,
    No
}
