using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Application.Query.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;

namespace TomasAI.IFM.Application.Query.RestApiClient.UnitTests
{
    public class FundQueryTests
    {
        [Fact]
        public async Task GetFundsAsync()
        {
            var queryServiceOptions = new QueryServiceRestApiOptions("http://localhost:35106");

            var queryService = new QueryServiceRestApiClient(queryServiceOptions, null);
            var client = new FundQueryApi(queryService);
            var funds = await client.GetFundsAsync();
            funds.Success.Should().BeTrue();
            funds.Value.Should().NotBeNull();

        }
    }
}
