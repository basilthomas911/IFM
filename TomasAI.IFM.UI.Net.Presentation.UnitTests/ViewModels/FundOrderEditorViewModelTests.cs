using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class FundOrderEditorViewModelTests
{
    [Fact]
    public void ManualOrderCreationContracts_DoNotExposeTradeOwnedFields()
    {
        var forbidden = new[]
        {
            "BaseContractId",
            "UnderlyingRoot",
            "TradeDate",
            "RequestedTradeDate",
            "MaturityDate",
            "RequestedMaturityDate",
        };

        typeof(ManualFundOrderDraftEditorModel).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(forbidden);
        typeof(CreateManualFundOrderRequest).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(forbidden);
    }
    [Fact]
    public async Task LoadOperation_PublishesIdentifierWithoutRequiringTradeDetails()
    {
        var subject = CreateSubject();

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.OrderId.Should().Be(501);
        subject.ViewModel.Reference.Should().BeEmpty();
        subject.ViewModel.CanSave.Should().BeTrue();
        subject.ViewModel.FundOrder.OrderId.Should().Be(501);
    }

    [Fact]
    public void MultilineReference_IsPreservedAndTrimmedOnlyAtPayloadBoundary()
    {
        var subject = CreateSubject();

        subject.ViewModel.SetReference("  First line\r\nSecond line  ");

        subject.ViewModel.Reference.Should().Be("  First line\r\nSecond line  ");
        subject.ViewModel.FundOrder.Reference.Should().Be("First line\r\nSecond line");
        subject.ViewModel.CanSave.Should().BeTrue();
    }

    [Fact]
    public async Task SaveRemainsAvailableWhileIdentifierAllocationIsRunning()
    {
        var completion = new TaskCompletionSource<ServiceResult<ScalarReadModel<int>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var subject = CreateSubject(orderIdResult: completion.Task);

        var first = subject.ViewModel.LoadOperation.ExecuteAsync();
        var second = subject.ViewModel.LoadOperation.ExecuteAsync();

        first.Should().BeSameAs(second);
        subject.ViewModel.IsBusy.Should().BeTrue();
        subject.ViewModel.CanSave.Should().BeTrue();

        completion.SetResult(new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(501)));
        await first;

        subject.ViewModel.IsBusy.Should().BeFalse();
        await subject.ReferenceApi.Received(1).GetNextSeedIdAsync("OrderId");
    }

    [Fact]
    public async Task CodedAllocationFailure_IsObservableButDoesNotDisableSave()
    {
        var subject = CreateSubject(
            orderIdResult: Task.FromResult<ServiceResult<ScalarReadModel<int>>>(
                new ServiceFailed<ScalarReadModel<int>>(919, "order id unavailable")));

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.LoadOperation.ExecuteAsync())
            .Should().ThrowAsync<UiOperationException>();

        exception.Which.ErrorCode.Should().Be(919);
        subject.ViewModel.LastError!.ErrorCode.Should().Be(919);
        subject.ViewModel.CanSave.Should().BeTrue();
        typeof(FundOrderEditorViewModel)
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
            .Should().BeEmpty();
        await subject.ViewModel.DisposeAsync();
    }

    static Subject CreateSubject(
        Task<ServiceResult<ScalarReadModel<int>>>? orderIdResult = null)
    {
        orderIdResult ??= Task.FromResult<ServiceResult<ScalarReadModel<int>>>(
            new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(501)));
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        referenceApi.GetNextSeedIdAsync("OrderId").Returns(_ => orderIdResult);
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero));
        var viewModel = new FundOrderEditorViewModel(
            17,
            UiServiceFactory.CreateReference(referenceApi),
            timeProvider);
        return new Subject(viewModel, referenceApi);
    }

    sealed record Subject(
        FundOrderEditorViewModel ViewModel,
        IReferenceQueryApi ReferenceApi);
}