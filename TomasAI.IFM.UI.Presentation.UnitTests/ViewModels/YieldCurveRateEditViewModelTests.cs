using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Presentation.UnitTests.ViewModels;

public class YieldCurveRateEditViewModelTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CheckValueDateOperation_PublishesDuplicateAndSaveState(
        bool exists,
        bool expectedCanSave)
    {
        var (viewModel, api) = CreateSubject();
        var valueDate = new DateOnly(2026, 8, 11);
        api.YieldCurveRateExistsAsync(valueDate).Returns(
            new ServiceOk<ScalarReadModel<bool>>(new ScalarReadModel<bool>(exists)));
        viewModel.SetValueDate(valueDate);

        await viewModel.CheckValueDateOperation.ExecuteAsync();

        viewModel.RateExists.Should().Be(exists);
        viewModel.CanSave.Should().Be(expectedCanSave);
    }

    [Fact]
    public async Task CheckValueDateOperation_PreservesCodedQueryFailure()
    {
        var (viewModel, api) = CreateSubject();
        var valueDate = new DateOnly(2026, 8, 11);
        api.YieldCurveRateExistsAsync(valueDate).Returns(
            new ServiceFailed<ScalarReadModel<bool>>(812, "validation unavailable"));
        viewModel.SetValueDate(valueDate);

        var exception = await FluentActions.Awaiting(
                () => viewModel.CheckValueDateOperation.ExecuteAsync())
            .Should().ThrowAsync<ModelOperationException>();

        exception.Which.ErrorCode.Should().Be(812);
        viewModel.CanSave.Should().BeFalse();
        viewModel.CheckValueDateOperation.LastFailure.Should().BeSameAs(exception.Which);
    }

    static (YieldCurveRateEditViewModel ViewModel, IMarketDataQueryApi Api) CreateSubject()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var feedApi = Substitute.For<IMarketDataFeedQueryApi>();
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<MarketDataQueryModel>().Returns(new MarketDataQueryModel(api, feedApi));
        return (new YieldCurveRateEditViewModel(appRoot), api);
    }
}
