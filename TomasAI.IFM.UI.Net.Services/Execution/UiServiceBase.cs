using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Services.Execution;

/// <summary>
/// Provides a base implementation for UI services that support error handling and asynchronous execution of
/// functions, queries, and commands.
/// </summary>
/// <remarks>This class includes mechanisms for handling errors through a notifier delegate, as well as methods
/// for executing asynchronous operations such as service functions, queries, and commands. Derived classes can use
/// these methods to simplify error handling and execution flow.</remarks>
/// <typeparam name="TService">The concrete UI service type.</typeparam>
public class UiServiceBase<TService>
    : IUiService<TService> where TService : class
{
    readonly AsyncLocal<Action<int, string>?> _errorNotifier = new();

    /// <summary>
    /// execute action when service function returns an error
    /// </summary>
    /// <param name="errorNotifier"></param>
    public void OnError(Action<int, string> errorNotifier = null!)
        => _errorNotifier.Value = errorNotifier;

    /// <summary>
    /// raise error
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    public void RaiseError(int errorCode, string errorMsg)
        => _errorNotifier.Value?.Invoke(errorCode, errorMsg);

    /// <summary>
    /// execute async lambda function
    /// </summary>
    /// <param name="serviceFunc"></param>
    /// <returns></returns>
    public Task ExecuteAsync(Func<Task> serviceFunc)
    {
        ArgumentNullException.ThrowIfNull(serviceFunc);
        return serviceFunc();
    }

    /// <summary>
    /// execute async lambda function
    /// </summary>
    /// <param name="serviceFunc"></param>
    /// <returns></returns>
    public ValueTask ExecuteValueTaskAsync(Func<ValueTask> serviceFunc)
    {
        ArgumentNullException.ThrowIfNull(serviceFunc);
        return serviceFunc();
    }

    /// <summary>
    /// execute query
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="serviceQuery"></param>
    /// <param name="resultAction"></param>
    /// <returns></returns>
    protected async Task ExecuteAsync<TResult>(Func<Task<ServiceResult<TResult>>> serviceQuery, Action<TResult> resultAction)
    {
        ArgumentNullException.ThrowIfNull(serviceQuery);
        ArgumentNullException.ThrowIfNull(resultAction);

        var serviceResult = await serviceQuery();
        if (serviceResult.Success)
            resultAction(serviceResult.Value!);
        else
            RaiseError(serviceResult.ErrorCode, serviceResult.ErrorMessage);
    }

    /// <summary>
    /// Executes a service query and awaits asynchronous result processing before reporting completion.
    /// </summary>
    protected async Task ExecuteAsync<TResult>(
        Func<Task<ServiceResult<TResult>>> serviceQuery,
        Func<TResult, Task> resultOperation)
    {
        ArgumentNullException.ThrowIfNull(serviceQuery);
        ArgumentNullException.ThrowIfNull(resultOperation);

        var serviceResult = await serviceQuery();
        if (serviceResult.Success)
            await resultOperation(serviceResult.Value!);
        else
            RaiseError(serviceResult.ErrorCode, serviceResult.ErrorMessage);
    }

    /// <summary>
    /// execute command
    /// </summary>
    /// <typeparam name="Guid"></typeparam>
    /// <param name="funcCommand"></param>
    /// <returns></returns>
    protected async Task<Guid> ExecuteCommandAsync<Guid>(Func<Task<ServiceResult<Guid>>> funcCommand, Action onCompleted = null!)
    {
        ArgumentNullException.ThrowIfNull(funcCommand);
        var commandId = default(Guid);
        var serviceResult = await funcCommand();
        if (serviceResult?.Success == true)
        {
            commandId = serviceResult.Value;
            onCompleted?.Invoke();
        }
        else
            RaiseError(serviceResult?.ErrorCode ?? 0, serviceResult?.ErrorMessage ?? "Unknown error");

        return commandId!;
    }

}

/// <summary>Provides the UiServiceExecution UI service boundary.</summary>
public static class UiServiceExecution
{
    /// <summary>
    /// Executes an asynchronous operation on the concrete model instance.
    /// </summary>
    /// <remarks>Completion, cancellation, and failures are returned to the caller. This prevents an asynchronous
    /// lambda from being converted to <see cref="Action{T}"/> and losing observable completion.</remarks>
    /// <typeparam name="TModel">The type of the model, which must be a reference type.</typeparam>
    /// <param name="model">The model instance on which the action will be performed. Cannot be <see langword="null"/>.</param>
    /// <param name="operation">The asynchronous operation to execute. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token that cancels execution before the operation starts.</param>
    /// <returns>A task representing the complete operation.</returns>
    public static Task ExecuteAsync<TModel>(
        this IUiService<TModel> model,
        Func<TModel, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        if (model is not TModel concreteModel)
        {
            throw new InvalidOperationException(
                $"Model instance '{model.GetType().FullName}' does not implement its declared concrete type '{typeof(TModel).FullName}'.");
        }

        return operation(concreteModel, cancellationToken);
    }

    /// <summary>
    /// Executes an asynchronous operation on the concrete model instance when the operation does not consume a
    /// cancellation token directly.
    /// </summary>
    /// <typeparam name="TModel">The type of the model, which must be a reference type.</typeparam>
    /// <param name="model">The model instance on which the operation will be performed.</param>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="cancellationToken">A token that cancels execution before the operation starts.</param>
    /// <returns>A task representing the complete operation.</returns>
    public static Task ExecuteAsync<TModel>(
        this IUiService<TModel> model,
        Func<TModel, Task> operation,
        CancellationToken cancellationToken = default)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(operation);
        return model.ExecuteAsync((concreteModel, _) => operation(concreteModel), cancellationToken);
    }

    /// <summary>
    /// Executes a query action on the specified model and returns the result.
    /// </summary>
    /// <remarks>Invalid model casts and operation failures are propagated to the caller.</remarks>
    /// <typeparam name="TModel">The type of the model on which the query is executed. Must be a reference type.</typeparam>
    /// <typeparam name="TData">The type of the data returned by the query action.</typeparam>
    /// <param name="model">The model instance on which the query action is performed. Cannot be null.</param>
    /// <param name="viewAction">A function that defines the query action to execute on the model. Cannot be null.</param>
    /// <returns>The result of the query action.</returns>
    public static TData ExecuteQuery<TModel, TData>(this IUiService<TModel> model, Func<TModel, TData> viewAction) where TModel : class
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(viewAction);

        if (model is not TModel concreteModel)
        {
            throw new InvalidOperationException(
                $"Model instance '{model.GetType().FullName}' does not implement its declared concrete type '{typeof(TModel).FullName}'.");
        }

        return viewAction(concreteModel);
    }

}
