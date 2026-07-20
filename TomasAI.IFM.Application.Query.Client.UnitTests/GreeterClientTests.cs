using System;
using System.Threading.Tasks;
using Xunit;
using TomasAI.IFM.Application.Query.Client;

namespace TomasAI.IFM.Application.Query.Client.UnitTests
{
    public class GreeterClientTests
    {
        [Fact]
        public async Task SayHelloAsync()
        {
            var client = new GreeterClient();
            await client.SayHelloAsync();
        }
    }
}
