using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Query.Client;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.RestApiClient.UnitTests
{
    public class TradePlanQueryTests
    {
        [Fact]
        public async Task GetTradePlanActionAsync()
        {
            /// given a query service...
            var controller = "TestController";
            var queryService = Substitute.For<IQueryService>();
            var tradePlanSummary = new List<TradePlanActionReadModel>().ToArray();
            queryService.PostApiQueryAsync(query: Arg.Any<IQuery<TradePlanActionReadModel[]>>(), controllerName: controller)
                .Returns(new ServiceOk<TradePlanActionReadModel[]>(tradePlanSummary));
            var tradePlanQuery = new TradePlanQueryApi(queryService);

            /// when executing trade plan query GetTradePlanSummaryAsync...
            var result = await tradePlanQuery.GetTradePlanActionAsync(1001, 2002, new DateOnly(2025,02,02));

            /// then a valid result will be returned.
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

       

    }
}
