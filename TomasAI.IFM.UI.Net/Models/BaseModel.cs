using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// Provides a base implementation for models that support error handling and asynchronous execution of service
/// functions, queries, and commands.
/// </summary>
/// <remarks>This class includes mechanisms for handling errors through a notifier delegate, as well as methods
/// for executing asynchronous operations such as service functions, queries, and commands. Derived classes can use
/// these methods to simplify error handling and execution flow.</remarks>
/// <typeparam name="TModel">The type of the model that this base class represents. Must be a reference type.</typeparam>
public class BaseModel<TModel>
    : IModel<TModel> where TModel : class
{
    Action<int, string>? _errorNotifier;

    /// <summary>
    /// execute action when service function returns an error
    /// </summary>
    /// <param name="errorNotifier"></param>
    public void OnError(Action<int, string> errorNotifier = null!) 
        => _errorNotifier = errorNotifier;

    /// <summary>
    /// raise error
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    public void RaiseError(int errorCode, string errorMsg) 
        => _errorNotifier?.Invoke(errorCode, errorMsg);

    /// <summary>
    /// execute async lambda function 
    /// </summary>
    /// <param name="serviceFunc"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(Func<Task> serviceFunc)
    {
        try
        {
            await serviceFunc();
        }
        catch (Exception ex)
        {
            if (ex is not ThreadAbortException)
                RaiseError(9999, $"{ex}");
        }
    }

    /// <summary>
    /// execute async lambda function 
    /// </summary>
    /// <param name="serviceFunc"></param>
    /// <returns></returns>
    public async ValueTask ExecuteValueTaskAsync(Func<ValueTask> serviceFunc)
    {
        try
        {
            await serviceFunc();
        }
        catch (Exception ex)
        {
            if (ex is not ThreadAbortException)
                RaiseError(99991, $"{ex}");
        }
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
        try
        {
            var serviceResult = await serviceQuery();
            if (serviceResult.Success)
                resultAction?.Invoke(serviceResult.Value!);
            else
                RaiseError(serviceResult.ErrorCode, serviceResult.ErrorMessage);
        }
        catch (Exception ex)
        {
            RaiseError(1237, $"{ex}");
        }
    }

    /// <summary>
    /// execute command 
    /// </summary>
    /// <typeparam name="Guid"></typeparam>
    /// <param name="funcCommand"></param>
    /// <returns></returns>
    protected async Task<Guid> ExecuteCommandAsync<Guid>(Func<Task<ServiceResult<Guid>>> funcCommand, Action onCompleted = null!)
    {
        var commandId = default(Guid);
        try
        {
            var serviceResult = await funcCommand();
            if (serviceResult?.Success == true)
            {
                commandId = serviceResult.Value;
                onCompleted?.Invoke();
            }
            else
                RaiseError(serviceResult?.ErrorCode ?? 0, serviceResult?.ErrorMessage ?? "Unknown error");
        }
        catch(Exception ex)
        {
            RaiseError(1238, $"{ex}");
        }
        return commandId!;
    }

}

public static class BaseModelExtension
{
    /// <summary>
    /// Executes the specified action on the model instance.
    /// </summary>
    /// <remarks>This method attempts to cast the model to the specified type <typeparamref name="TModel"/>
    /// and invokes the provided action. Any exceptions thrown during the execution of the action are caught and
    /// suppressed.</remarks>
    /// <typeparam name="TModel">The type of the model, which must be a reference type.</typeparam>
    /// <param name="model">The model instance on which the action will be performed. Cannot be <see langword="null"/>.</param>
    /// <param name="viewAction">The action to execute on the model. Cannot be <see langword="null"/>.</param>
    public static void Execute<TModel>(this IModel<TModel> model, Action<TModel> viewAction) where TModel : class
    {
        try
        {
            viewAction((model as TModel)!);
        }
        catch { }
    }

    /// <summary>
    /// Executes a query action on the specified model and returns the result.
    /// </summary>
    /// <remarks>This method attempts to execute the provided <paramref name="viewAction"/> on the given
    /// <paramref name="model"/>. If an exception is thrown during execution, the method suppresses the exception and
    /// returns the default value of <typeparamref name="TData"/>.</remarks>
    /// <typeparam name="TModel">The type of the model on which the query is executed. Must be a reference type.</typeparam>
    /// <typeparam name="TData">The type of the data returned by the query action.</typeparam>
    /// <param name="model">The model instance on which the query action is performed. Cannot be null.</param>
    /// <param name="viewAction">A function that defines the query action to execute on the model. Cannot be null.</param>
    /// <returns>The result of the query action, or the default value of <typeparamref name="TData"/> if an exception occurs.</returns>
    public static TData ExecuteQuery<TModel, TData>(this IModel<TModel> model, Func<TModel, TData> viewAction) where TModel : class
    {
        var result = default(TData);
        try
        {
            result = viewAction((model as TModel)!);
        }
        catch { }
        return result!;
    }

}
