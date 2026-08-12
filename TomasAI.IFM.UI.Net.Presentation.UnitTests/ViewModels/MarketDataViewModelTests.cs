using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class MarketDataViewModelTests
{
    [Fact]
    public async Task LoadOperation_PublishesDefinitionsAndSafeSelection()
    {
        var definition = Definition("FuturesContract", "Futures contracts");
        var (viewModel, api) = CreateSubject(
            new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([definition])));

        await viewModel.LoadDefinitionTypesOperation.ExecuteAsync();

        viewModel.DefinitionTypes.Should().Equal(definition);
        viewModel.GetDefinitionType(0).Should().BeSameAs(definition);
        viewModel.GetDefinitionType(-1).Should().BeNull();
        await api.Received(1).GetMarketDataDefinitionTypesAsync();
    }

    [Fact]
    public void SetEditorBusy_PublishesSemanticShellState()
    {
        var (viewModel, _) = CreateSubject(new ServiceOk<LookupTypeCollection>(new LookupTypeCollection()));
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.SetEditorBusy(true);
        viewModel.SetEditorBusy(false);

        changes.Should().Equal(nameof(viewModel.IsEditorBusy), nameof(viewModel.IsEditorBusy));
    }

    [Fact]
    public async Task LoadOperation_PreservesCodedFailure()
    {
        var (viewModel, _) = CreateSubject(
            new ServiceFailed<LookupTypeCollection>(821, "definitions unavailable"));

        var exception = await FluentActions.Awaiting(
                () => viewModel.LoadDefinitionTypesOperation.ExecuteAsync())
            .Should().ThrowAsync<ModelOperationException>();

        exception.Which.ErrorCode.Should().Be(821);
        viewModel.LoadDefinitionTypesOperation.LastFailure.Should().BeSameAs(exception.Which);
    }

    static (MarketDataViewModel ViewModel, IReferenceQueryApi Api) CreateSubject(
        ServiceResult<LookupTypeCollection> result)
    {
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetMarketDataDefinitionTypesAsync().Returns(Task.FromResult(result));
        var model = new ReferenceQueryModel(api);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<ReferenceQueryModel>().Returns(model);
        return (new MarketDataViewModel(appRoot), api);
    }

    static LookupTypeReadModel Definition(string shortCode, string description)
        => new("MarketDataDefinitionType", shortCode, 1, description, DateTime.UtcNow, "test");
}
