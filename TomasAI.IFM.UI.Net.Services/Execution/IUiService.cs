namespace TomasAI.IFM.UI.Net.Services.Execution
{
    /// <summary>Defines observable error reporting for a UI-facing domain service.</summary>
    /// <typeparam name="TService">The concrete service type.</typeparam>
    public interface IUiService<TService> where TService : class
    {
        /// <summary>Sets the callback that receives coded backend failures.</summary>
        /// <param name="errorNotifier">The callback, or <see langword="null"/> to clear it.</param>
        void OnError(Action<int, string> errorNotifier);
    }
}
