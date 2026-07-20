using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Application.Query.Client.Api;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.ViewModels;

namespace TomasAI.IFM.Application.Query.Client.UnitTests
{
    public class FundQueryTests
    {
        [Fact]
        public async Task GetFundsAsync()
        {
            var client = new FundQueryApi("https://localhost:5001");
            var funds = await client.GetFundsAsync();
            funds.Success.Should().BeTrue();
            funds.Value.Should().NotBeNull();
        }
    }
}
