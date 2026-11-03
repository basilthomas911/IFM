using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.Extensions;

/// <summary>
/// Converts a UI service error callback into observable asynchronous completion for ViewModels.
/// </summary>
public static class ObservableUiServiceExtensions
{

    extension<TModel>(IUiService<TModel> model)
        where TModel : class
    {
        /// <summary>
        /// Executes a UI service operation that does not consume its cancellation token directly.
        /// </summary>
        public Task ExecuteObservableAsync(
            Func<TModel, Task> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            return model.ExecuteObservableAsync(
                (concreteModel, operationCancellation) =>
                    operation(concreteModel).WaitAsync(operationCancellation),
                cancellationToken);
        }

        /// <summary>
        /// Executes a UI service operation and throws a coded exception when the service reports a failed result.
        /// </summary>
        public async Task ExecuteObservableAsync(
            Func<TModel, CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(operation);

            UiServiceOperationException? failure = null;
            model.OnError((errorCode, errorMessage) =>
                failure = new UiServiceOperationException(errorCode, errorMessage));

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
}
