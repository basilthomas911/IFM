using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Configuration;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.WebService
{
    public class RestApiService : IRestApiService
    {
        private readonly Uri _baseUri;

        /// <summary>
        /// create web api service
        /// </summary>
        public RestApiService(Uri baseUri)
            =>  _baseUri = baseUri;

        /// <summary>
        /// execute rest api action calls
        /// </summary>
        /// <param name="executeAction"></param>
        public void ExecuteRestApi(Action executeAction)
            => executeAction();

        /// <summary>
        /// execute rest api function calls
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="executeAction"></param>
        /// <returns></returns>
        public TResult ExecuteRestApi<TResult>(Action<ResultValue<TResult>> executeAction)
        {
            var result = new ResultValue<TResult>();
            executeAction(result);
            return result.ReturnValue;
        }

        public async Task<ServiceResult> PostAsync<TBody>(string postUri, TBody content)
             => await RestServiceCallAsync<ServiceResult>(async (client, result) => {
                 try
                 {
                     var response = await client.PostAsJsonAsync(postUri, content);
                     result.ReturnValue = GetServiceResult(response);
                 }
                 catch (Exception ex)
                 {
                     result.ReturnValue = GetExceptionResult(ex);
                 }
             });
        /// <summary>
        /// return content from selected uri
        /// </summary>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="getUri"></param>
        /// <returns></returns>
        protected ServiceResult<TOut> Get<TOut>(string getUri)
           => RestServiceCall<ServiceResult<TOut>>((client, result) => {
                   try
                   {
                       var response =  client.GetAsync(getUri).Result;
                       result.ReturnValue = GetServiceResult<TOut>(response);
                   }
                   catch (Exception ex)
                   {
                       result.ReturnValue = GetExceptionResult<TOut>(ex);
                   }
               });
        

        /// <summary>
        /// execute post action to selected uri with no content
        /// </summary>
        /// <param name="postUri"></param>
        /// <returns></returns>
        protected ServiceResult Post(string postUri)
             => RestServiceCall<ServiceResult>((client, result) => {
                 try
                 {
                     var response = client.PostAsync(postUri, default(HttpContent)).Result;
                     result.ReturnValue = GetServiceResult(response);
                 }
                 catch (Exception ex)
                 {
                     result.ReturnValue = GetExceptionResult(ex);
                 }
             });

        /// <summary>
        /// execute post action with content in thew body and with no expected return content
        /// </summary>
        /// <typeparam name="TBody"></typeparam>
        /// <param name="postUri"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        protected ServiceResult Post<TBody>(string postUri, TBody content)
             => RestServiceCall<ServiceResult>((client, result) => {
                 try
                 {
                     var response = client.PostAsJsonAsync(postUri, content).Result;
                     result.ReturnValue = GetServiceResult(response);
                 }
                 catch (Exception ex)
                 {
                     result.ReturnValue = GetExceptionResult(ex);
                 }
             });


        /// <summary>
        /// execute post action with content in body and expecting a return value 
        /// </summary>
        /// <typeparam name="TBody"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="postUri"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        protected ServiceResult<TOut> Post<TBody, TOut>(string postUri, TBody content)
             => RestServiceCall<ServiceResult<TOut>>((client, result) => {
                 try
                 {
                     var response = client.PostAsJsonAsync(postUri, content).Result;
                     result.ReturnValue = GetServiceResult<TOut>(response );
                 }
                 catch (Exception ex)
                 {
                     result.ReturnValue = GetExceptionResult<TOut>(ex);
                 }
             });

        

        /// <summary>
        /// execute put action with content in body
        /// </summary>
        /// <typeparam name="TBody"></typeparam>
        /// <param name="postUri"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        protected ServiceResult Put<TBody>(string putUri, TBody content)
            => RestServiceCall<ServiceResult>((client, result) => {
                try
                {
                    var response = client.PutAsJsonAsync(putUri, content).Result;
                    result.ReturnValue = GetServiceResult(response);
                }
                catch (Exception ex)
                {
                    result.ReturnValue = GetExceptionResult(ex);
                }
            });


        /// <summary>
        /// execute delete action with selected uri
        /// </summary>
        /// <param name="postUri"></param>
        /// <returns></returns>
        protected ServiceResult Delete(string deleteUri)
           => RestServiceCall<ServiceResult>((client, result) => {
               try
               {
                   var response = client.DeleteAsync(deleteUri).Result;
                   switch (response.StatusCode)
                   {
                       case HttpStatusCode.OK:
                           result.ReturnValue = new ServiceOk();
                           break;
                       case HttpStatusCode.Conflict:
                           result.ReturnValue = response.Content.ReadAsAsync<ServiceFailed>().Result;
                           break;
                       default:
                           result.ReturnValue = new ServiceFailed(-1, "Unknown Server Error");
                           break;
                   }
               }
               catch (Exception ex)
               {
                   result.ReturnValue = GetExceptionResult(ex);
               }
           });

        /// <summary>
        /// call base rest service call
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="serviceAction"></param>
        /// <returns></returns>
        private TResult RestServiceCall<TResult>(Action<HttpClient, ResultValue<TResult>> serviceAction)
        {
            var result = default(TResult);
            using (var handler = new HttpClientHandler { UseDefaultCredentials = true })
            {
                using (var client = new HttpClient(handler))
                {
                    client.BaseAddress = _baseUri;
                    client.Timeout = TimeSpan.FromMinutes(10);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var serviceResult = new ResultValue<TResult>();
                    serviceAction(client, serviceResult);
                    result = serviceResult.ReturnValue;
                }
            }
            return result;
        }

        /// <summary>
        /// call base rest service call
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="serviceAction"></param>
        /// <returns></returns>
        private async Task<TResult> RestServiceCallAsync<TResult>(Func<HttpClient, ResultValue<TResult>, Task> serviceAction)
        {
            var result = default(TResult);
            using (var handler = new HttpClientHandler { UseDefaultCredentials = true })
            {
                using (var client = new HttpClient(handler))
                {
                    client.BaseAddress = _baseUri;
                    client.Timeout = TimeSpan.FromMinutes(10);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var serviceResult = new ResultValue<TResult>();
                    await serviceAction(client, serviceResult);
                    result = serviceResult.ReturnValue;
                }
            }
            return result;
        }

        /// <summary>
        /// extract service call result from service response
        /// </summary>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="response"></param>
        /// <returns></returns>
        private ServiceResult<TOut> GetServiceResult<TOut>(HttpResponseMessage response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return response.Content.ReadAsAsync<ServiceOk<TOut>>().Result;
                case HttpStatusCode.Conflict:
                    return response.Content.ReadAsAsync<ServiceFailed<TOut>>().Result;
            }
            return new ServiceFailed<TOut>(-1, "Unknown Server Error");
        }

        /// <summary>
        /// extract service call result from service call response
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        private ServiceResult GetServiceResult(HttpResponseMessage response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return response.Content.ReadAsAsync<ServiceOk>().Result;
                case HttpStatusCode.Conflict:
                    return response.Content.ReadAsAsync<ServiceFailed>().Result;
            }
            return new ServiceFailed(-1, "Unknown Server Error");
        }

        /// <summary>
        /// convert exception to failed servce call result
        /// </summary>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="ex"></param>
        /// <returns></returns>
        private ServiceResult<TOut> GetExceptionResult<TOut>(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return new ServiceFailed<TOut>(-2, ex.Message);
        }

        /// <summary>
        /// convert exception to failed service call result
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private ServiceResult GetExceptionResult(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return new ServiceFailed(-2, ex.Message);
        }
    }

    public class ResultValue<TValue>
    {
        public TValue ReturnValue { get; set; }
    }


}
