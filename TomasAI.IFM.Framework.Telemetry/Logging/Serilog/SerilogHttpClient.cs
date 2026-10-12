using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace TomasAI.IFM.Framework.Telemetry.Logging.Serilog
{
    public sealed class SerilogHttpClient : IHttpClient
    {
        readonly HttpClient _httpClient;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient owns and disposes its handler; construction uses the owning HttpClient constructor.")]
        public SerilogHttpClient() => _httpClient = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true, CheckCertificateRevocationList = true });

        public void Configure(IConfiguration configuration) 
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", configuration["apiKey"]);
        }

        public void Dispose() => _httpClient?.Dispose();

        public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentStream);
            try
            {
                contentStream.Position = 0;
                using var httpContent = new BorrowedLogStreamContent(contentStream);
                var response = await _httpClient
                    .PostAsync(new Uri(requestUri, UriKind.Absolute), httpContent, cancellationToken)
                    .ConfigureAwait(false);
                return response;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex);
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }
        }
    }
}
