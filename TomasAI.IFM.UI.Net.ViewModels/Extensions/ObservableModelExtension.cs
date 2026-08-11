using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.Extensions;

/// <summary>
/// Converts the legacy Model error callback into observable asynchronous completion for ViewModels.
/// </summary>
public static class ObservableModelExtension
{
    /// <summary>
    /// Executes a Model operation that does not consume its cancellation token directly.
    /// </summary>
    public static Task ExecuteObservableAsync<TModel>(
        this IModel<TModel> model,
        Func<TModel, Task> operation,
        CancellationToken cancellationToken = default)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(operation);
        return model.ExecuteObservableAsync(
            (concreteModel, _) => operation(concreteModel),
            cancellationToken);
    }

    /// <summary>
    /// Executes a Model operation and throws a coded exception when the Model reports a failed service result.
    /// </summary>
    public static async Task ExecuteObservableAsync<TModel>(
        this IModel<TModel> model,
        Func<TModel, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(operation);

        ModelOperationException? failure = null;
        model.OnError((errorCode, errorMessage) =>
            failure = new ModelOperationException(errorCode, errorMessage));

        try
        {
            await model.ExecuteAsync(operation, cancellationToken);
            if (failure is not null)
                throw failure;
        }
        finally
        {
            model.OnError(null!);
        }
    }
}
