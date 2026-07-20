using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Configuration;

namespace TomasAI.IFM.Shared.WebService
{
    public class WebApiService 
    {
        protected Uri BaseUri { get; set; }

        /// <summary>
        /// create web api service
        /// </summary>
        public WebApiService(Uri baseUri)
        {
            BaseUri = baseUri;
        }

        /// <summary>
        /// set base uri
        /// </summary>
        /// <param name="baseUri"></param>
        public void SetBaseUri(Uri baseUri)
        {
            BaseUri = baseUri;
        }

        /// <summary>
        /// delete resource located @ deleteUri
        /// </summary>
        /// <param name="deleteUri"></param>
        /// <returns></returns>
        /// <remarks>
        /// e.g. api/investor/1234
        /// </remarks>
        public async Task<TOut> DeleteAsync<TOut>(string deleteUri)
        {
            return await WebServiceCallAsync<TOut>(async client => 
            {
                var response = await client.DeleteAsync(deleteUri);
                response.EnsureSuccessStatusCode();
                return response.IsSuccessStatusCode ?
                   await response.Content.ReadAsAsync<TOut>() :
                   default(TOut);
            });
        }

        /// <summary>
        /// return data from resource @ getUri
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="getUri"></param>
        /// <returns>resource data</returns>
        /// <remarks>
        /// e.g. api/investor/1234
        /// </remarks>
        public async Task<TOut> GetAsync<TOut>(string getUri)
        {
            return await WebServiceCallAsync<TOut>(async client =>
            {
                var response = await client.GetAsync(getUri);
                response.EnsureSuccessStatusCode();
                return response.IsSuccessStatusCode ?
                   await response.Content.ReadAsAsync<TOut>() :
                   default(TOut);
            });
        }

        /// <summary>
        /// return data from resource @ getUri
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="getUri"></param>
        /// <returns>resource data</returns>
        /// <remarks>
        /// e.g. api/investor/1234
        /// </remarks>
        public TOut Get<TOut>(string getUri)
        {
            return WebServiceCall<TOut>(client =>
            {
                var response = client.GetAsync(getUri).Result;
                response.EnsureSuccessStatusCode();
                return response.IsSuccessStatusCode ?
                   response.Content.ReadAsAsync<TOut>().Result :
                   default(TOut);
            });
        }

        /// <summary>
        /// post data to resource @ postUri
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <param name="postUri"></param>
        /// <param name="postBody"></param>
        /// <returns></returns>
        public async Task<TOut> PostAsync<TIn,TOut>(string postUri, TIn postBody)
            => await WebServiceCallAsync<TOut>(async client => {
                var response = await client.PostAsJsonAsync<TIn>(postUri, postBody);
                response.EnsureSuccessStatusCode();
                return response.IsSuccessStatusCode ?
                   await response.Content.ReadAsAsync<TOut>() :
                   default(TOut);
            });

        

        /// <summary>
        /// post data to resource @ postUri
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <param name="postUri"></param>
        /// <param name="postBody"></param>
        /// <returns></returns>
        public TOut Post<TIn, TOut>(string postUri, TIn postBody)
        {
            var result = default(TOut);
            using (var client = new HttpClient())
            {
                client.BaseAddress = BaseUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = client.PostAsJsonAsync<TIn>(postUri, postBody).Result;
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                    result = response.Content.ReadAsAsync<TOut>().Result;
            }
            return result;
        } 

        /// <summary>
        /// modify resource @ putUri with updated data
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <param name="putUri"></param>
        /// <param name="putBody"></param>
        /// <returns></returns>
        public async Task<TOut> PutAsync<TIn, TOut>(string putUri, TIn putBody)
        {
            var result = default(TOut);
            using (var client = new HttpClient())
            {
                client.BaseAddress = BaseUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PutAsJsonAsync<TIn>(putUri, putBody);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                    result = await response.Content.ReadAsAsync<TOut>();
            }
            return result;
        }

        /// <summary>
        /// make web service call 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="serviceFunc"></param>
        /// <returns></returns>
        private TResult WebServiceCall<TResult>(Func<HttpClient, TResult> serviceFunc)
        {
            var result = default(TResult);
            using (var client = new HttpClient())
            {
                client.BaseAddress = BaseUri;
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                result = serviceFunc(client);
            }
            return result;
        }

        /// <summary>
        /// make web service call asynchronously
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="serviceFunc"></param>
        /// <returns></returns>
        private async Task<TResult> WebServiceCallAsync<TResult>( Func<HttpClient, Task<TResult>> serviceFunc)
        {
            var result = default(TResult);
            using (var client = new HttpClient())
            {
                client.BaseAddress = BaseUri;
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                result = await serviceFunc(client);
            }
            return result;
        }

        /// <summary>
        /// make web service call asynchronously
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="serviceFunc"></param>
        /// <returns></returns>
        private async Task WebServiceCallAsync<TResult>(Func<HttpClient, Task> serviceFunc)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = BaseUri;
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                await serviceFunc(client);
            }
        }

    }
}
