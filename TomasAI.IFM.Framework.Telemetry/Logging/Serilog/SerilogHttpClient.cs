using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace TomasAI.IFM.Framework.Telemetry.Logging.Serilog
{
    public class SerilogHttpClient : IHttpClient
    {
        readonly HttpClient _httpClient;
        public SerilogHttpClient() => _httpClient = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true });

        public void Configure(IConfiguration configuration) => _httpClient.DefaultRequestHeaders.Add("X-Api-Key", configuration["apiKey"]);

        public void Dispose() => _httpClient?.Dispose();

        public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream, CancellationToken cancellationToken)
        {
            try
            {
                contentStream.Position = 0;
                var httpContent = new StreamContent(contentStream);
                var response = await _httpClient
                    .PostAsync(requestUri, httpContent, cancellationToken)
                    .ConfigureAwait(false);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }
        }
    }
}
