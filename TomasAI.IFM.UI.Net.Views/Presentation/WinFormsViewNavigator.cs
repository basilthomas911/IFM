using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>
/// Resolves and presents WinForms views while exposing only framework-neutral navigation contracts.
/// </summary>
public sealed class WinFormsViewNavigator(Func<Type, object> resolveView) : IViewNavigator
{
    readonly Func<Type, object> _resolveView = resolveView ?? throw new ArgumentNullException(nameof(resolveView));

    public TView CreateView<TView>() where TView : class
    {
        var view = (TView)_resolveView(typeof(TView));
        if (view is Control control)
            control.ApplyEasternTimeDisplayPolicy();
        return view;
    }

    public NavigationResult ShowModal<TView>(Action<TView>? initialize = null) where TView : class
    {
        var view = CreateView<TView>();
        initialize?.Invoke(view);
        if (view is not Form form)
            throw new InvalidOperationException($"View '{typeof(TView).FullName}' is not a WinForms Form.");

        var owner = Form.ActiveForm
            ?? Application.OpenForms
                .Cast<Form>()
                .FirstOrDefault(candidate => candidate.Visible && !ReferenceEquals(candidate, form));
        var result = owner is null
            ? form.ShowDialog()
            : form.ShowDialog(owner);

        return result switch
        {
            DialogResult.OK => NavigationResult.Accepted,
            DialogResult.Cancel => NavigationResult.Cancelled,
            DialogResult.Yes => NavigationResult.Yes,
            DialogResult.No => NavigationResult.No,
            _ => NavigationResult.None
        };
    }
}
