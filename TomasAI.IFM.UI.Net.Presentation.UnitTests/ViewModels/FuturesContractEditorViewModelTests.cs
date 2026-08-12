using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class FuturesContractEditorViewModelTests
{
    [Fact]
    public async Task LoadOperation_PublishesOneCompleteEditorSnapshot()
    {
        var contract = CreateContract("ES20260918");
        var subject = CreateSubject([contract]);

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.AllLookupTypesLoaded.Should().BeTrue();
        subject.ViewModel.SecurityTypes.Should().ContainSingle();
        subject.ViewModel.Currencies.Should().ContainSingle();
        subject.ViewModel.Exchanges.Should().ContainSingle();
        subject.ViewModel.Multipliers.Should().ContainSingle();
        subject.ViewModel.Symbols.Should().ContainSingle();
        subject.ViewModel.CurrentlyTraded.Should().Equal("Yes", "No");
        subject.ViewModel.FuturesContracts.Should().Equal(contract);
        subject.ViewModel.GetFuturesContract(-1).Should().BeNull();
        subject.ViewModel.GetContractMonth(12).Should().Be("Z");
    }

    [Fact]
    public async Task AddOperation_IsGuardedAndRefreshesTheContractSnapshot()
    {
        var existing = CreateContract("ES20260918");
        var added = CreateContract("ES20261218");
        var subject = CreateSubject([existing]);
        subject.QueryApi.GetFuturesContractsAsync().Returns(
            new ServiceOk<FuturesContractV2ReadModel[]>([existing]),
            new ServiceOk<FuturesContractV2ReadModel[]>([existing, added]));
        subject.CommandApi.AddFuturesContractAsync(added, true).Returns(
            new ServiceOk<Guid>(Guid.NewGuid()));

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.AddOperation.CanExecute.Should().BeFalse();
        subject.ViewModel.PrepareAdd(added);
        subject.ViewModel.AddOperation.CanExecute.Should().BeTrue();

        await subject.ViewModel.AddOperation.ExecuteAsync();

        subject.ViewModel.FuturesContracts.Should().Equal(existing, added);
        subject.ViewModel.LastStatusMessage.Should().Contain(added.ContractId);
        subject.ViewModel.AddOperation.CanExecute.Should().BeFalse();
        await subject.CommandApi.Received(1).AddFuturesContractAsync(added, true);
    }

    [Fact]
    public async Task LoadOperation_PreservesCodedReferenceFailure()
    {
        var subject = CreateSubject([]);
        subject.ReferenceApi.GetLookupTypesAsync("SecurityType").Returns(
            new ServiceFailed<LookupTypeCollection>(611, "security types unavailable"));

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.LoadOperation.ExecuteAsync())
            .Should().ThrowAsync<ModelOperationException>();

        exception.Which.ErrorCode.Should().Be(611);
        subject.ViewModel.LoadOperation.LastFailure.Should().BeSameAs(exception.Which);
        subject.ViewModel.AllLookupTypesLoaded.Should().BeFalse();
    }

    [Fact]
    public void ViewModel_DeclaresObservableStateWithoutViewCallbackDelegates()
    {
        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(FuturesContractEditorViewModel))
            .Should().BeTrue();

        var callbacks = typeof(FuturesContractEditorViewModel)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            });

        callbacks.Should().BeEmpty();
    }

    static Subject CreateSubject(FuturesContractV2ReadModel[] contracts)
    {
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        ConfigureLookup(referenceApi, "SecurityType", "FUT");
        ConfigureLookup(referenceApi, "Currency", "USD");
        ConfigureLookup(referenceApi, "Exchange", "CME");
        ConfigureLookup(referenceApi, "Multiplier", "50");
        ConfigureLookup(referenceApi, "Symbol", "ES");

        var queryApi = Substitute.For<IMarketDataQueryApi>();
        queryApi.GetFuturesContractsAsync().Returns(
            new ServiceOk<FuturesContractV2ReadModel[]>(contracts));
        var commandApi = Substitute.For<IMarketDataCommandApi>();
        var feedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<ReferenceQueryModel>().Returns(new ReferenceQueryModel(referenceApi));
        appRoot.GetModel<MarketDataQueryModel>().Returns(new MarketDataQueryModel(queryApi, feedQueryApi));
        appRoot.GetModel<MarketDataCommandModel>().Returns(new MarketDataCommandModel(commandApi));

        return new Subject(
            new FuturesContractEditorViewModel(appRoot),
            referenceApi,
            queryApi,
            commandApi);
    }

    static void ConfigureLookup(IReferenceQueryApi api, string name, string shortCode)
    {
        var lookup = new LookupTypeReadModel(
            name, shortCode, 0, shortCode, DateTime.UtcNow, "test");
        api.GetLookupTypesAsync(name).Returns(
            new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([lookup])));
    }

    static FuturesContractV2ReadModel CreateContract(string contractId)
        => new(
            contractId,
            $"{contractId} contract",
            "ES",
            contractId,
            "FUT",
            "USD",
            "CME",
            "50",
            new DateOnly(2026, 9, 18),
            true);

    sealed record Subject(
        FuturesContractEditorViewModel ViewModel,
        IReferenceQueryApi ReferenceApi,
        IMarketDataQueryApi QueryApi,
        IMarketDataCommandApi CommandApi);
}
