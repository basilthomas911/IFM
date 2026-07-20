using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Models

{
    public class BaseModel<TModel> : IModel<TModel> where TModel : class
    {
        private Action<int, string> _errorNotifier;

        /// <summary>
        /// execute action when service function returns an error
        /// </summary>
        /// <param name="errorNotifier"></param>
        public void OnError(Action<int, string> errorNotifier = null) => _errorNotifier = errorNotifier;

        /// <summary>
        /// raise error
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="errorMsg"></param>
        public void RaiseError(int errorCode, string errorMsg) => _errorNotifier?.Invoke(errorCode, errorMsg);

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
                if (!(ex is ThreadAbortException))
                    RaiseError(9999, $"{ex}");
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
                    resultAction?.Invoke(serviceResult.Value);
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
        protected async Task<Guid> ExecuteCommandAsync<Guid>(Func<Task<ServiceResult<Guid>>> funcCommand, Action onCompleted = null)
        {
            var commandId = default(Guid);
            try
            {
                var serviceResult = await funcCommand();
                if (serviceResult.Success)
                {
                    commandId = serviceResult.Value;
                    onCompleted?.Invoke();
                }
                else
                    RaiseError(serviceResult.ErrorCode, serviceResult.ErrorMessage);
            }
            catch(Exception ex)
            {
                RaiseError(1238, $"{ex}");
            }
            return commandId;
        }
  
    }

    public static class BaseModelExtension
    {

        public static void Execute<TModel>(this IModel<TModel> model, Action<TModel> viewAction) where TModel : class
        {
            try
            {
                viewAction(model as TModel);
            }
            catch { }
        }

        public static TData ExecuteQuery<TModel, TData>(this IModel<TModel> model, Func<TModel, TData> viewAction) where TModel : class
        {
            var result = default(TData);
            try
            {
                result = viewAction(model as TModel);
            }
            catch { }
            return result;
        }

    }
}
