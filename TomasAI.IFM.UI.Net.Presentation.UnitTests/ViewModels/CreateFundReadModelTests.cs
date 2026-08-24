using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Fund;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class CreateFundReadModelTests
{
    [Fact]
    public async Task LoadNewFundIdOperation_PublishesObservableIdentifier()
    {
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetNextSeedIdAsync("FundId").Returns(
            Task.FromResult<ServiceResult<ScalarReadModel<int>>>(
                new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(81))));
        var appRoot = Substitute.For<IAppRoot>();
        var viewModel = new CreateFundReadModel(appRoot, UiServiceFactory.CreateReference(api));

        await viewModel.LoadNewFundIdOperation.ExecuteAsync();

        viewModel.NewFundId.Should().Be(81);
        viewModel.CreateFundOperation.CanExecute.Should().BeFalse();
    }
}
