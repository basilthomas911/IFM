using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Reference;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class ReferenceViewModelTests
{
    [Fact]
    public async Task LoadOperation_PublishesObservableSelectorState()
    {
        var definition = Definition("LookupTypes", "lookup type definitions");
        var (viewModel, api) = CreateSubject(
            new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([
                ToBackend(Definition("EconomicCalendar", "economic calendar definitions")),
                ToBackend(definition)])));
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        await viewModel.LoadReferenceDataDefinitionTypesOperation.ExecuteAsync();

        viewModel.ReferenceDataDefinitionTypes.Should().Equal(definition);
        viewModel.GetReferenceDataDefinitionType(0).Should().Be(definition);
        viewModel.GetReferenceDataDefinitionType(-1).Should().BeNull();
        viewModel.GetReferenceDataDefinitionType(1).Should().BeNull();
        changes.Should().Contain(nameof(viewModel.ReferenceDataDefinitionTypes));
        await api.Received(1).GetReferenceDataDefinitionTypesAsync();
    }

    [Fact]
    public async Task LoadOperation_ConvertsCodedModelFailureToObservableFailure()
    {
        var (viewModel, _) = CreateSubject(
            new ServiceFailed<LookupTypeCollection>(742, "definitions unavailable"));

        var exception = await FluentActions
            .Awaiting(() => viewModel.LoadReferenceDataDefinitionTypesOperation.ExecuteAsync())
            .Should().ThrowAsync<UiOperationException>();

        exception.Which.ErrorCode.Should().Be(742);
        viewModel.LoadReferenceDataDefinitionTypesOperation.LastFailure.Should().BeSameAs(exception.Which);
        viewModel.ReferenceDataDefinitionTypes.Should().BeEmpty();
    }

    static (ReferenceViewModel ViewModel, IReferenceQueryApi Api) CreateSubject(
        ServiceResult<LookupTypeCollection> result)
    {
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetReferenceDataDefinitionTypesAsync().Returns(Task.FromResult(result));
        return (new ReferenceViewModel(UiServiceFactory.CreateReference(api)), api);
    }

    internal static LookupTypeUiModel Definition(string shortCode, string description)
        => new("ReferenceDataDefinitionType", shortCode, 1, description, DateTime.UtcNow, "test");

    internal static LookupTypeReadModel ToBackend(LookupTypeUiModel value)
        => new(value.LookupTypeName, value.ShortCode, value.OrderId, value.Description, value.CreatedOn, value.CreatedBy);
}
