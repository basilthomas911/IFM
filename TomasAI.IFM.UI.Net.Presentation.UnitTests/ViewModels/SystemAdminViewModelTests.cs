using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class SystemAdminViewModelTests
{
    [Fact]
    public async Task LoadOperation_PublishesObservableFunctionTypes()
    {
        var function = ReferenceViewModelTests.Definition("BackupDatabases", "Backup databases");
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetSystemAdminFunctionTypesAsync().Returns(
            Task.FromResult<ServiceResult<LookupTypeCollection>>(
                new ServiceOk<LookupTypeCollection>(new LookupTypeCollection(
                    [ReferenceViewModelTests.ToBackend(function)]))));
        var viewModel = new SystemAdminViewModel(UiServiceFactory.CreateReference(api));

        await viewModel.LoadFunctionTypesOperation.ExecuteAsync();

        viewModel.FunctionTypes.Should().Equal(function);
        viewModel.GetFunctionType(0).Should().Be(function);
        viewModel.GetFunctionType(1).Should().BeNull();
        await api.Received(1).GetSystemAdminFunctionTypesAsync();
    }
}
