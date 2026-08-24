using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

/// <summary>Verifies Fund query service failure mapping.</summary>
public sealed class FundQueryServiceTests
{
    [Fact]
    public async Task GetFundsAsync_ReportsCodedFailureWithoutPublishingData()
    {
        var api = Substitute.For<IFundQueryApi>();
        api.GetFundsAsync().Returns(Task.FromResult<ServiceResult<FundReadModel[]>>(
            new ServiceFailed<FundReadModel[]>(801, "fund query unavailable")));
        var model = new FundQueryService(api);
        var published = false;
        var errorCode = 0;
        model.OnError((code, _) => errorCode = code);

        await model.GetFundsAsync(_ => published = true);

        published.Should().BeFalse();
        errorCode.Should().Be(801);
    }
}
