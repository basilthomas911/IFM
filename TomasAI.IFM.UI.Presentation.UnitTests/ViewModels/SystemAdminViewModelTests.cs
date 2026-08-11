using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Presentation.UnitTests.ViewModels;

public class SystemAdminViewModelTests
{
    [Fact]
    public async Task LoadOperation_PublishesObservableFunctionTypes()
    {
        var function = ReferenceViewModelTests.Definition("BackupDatabases", "Backup databases");
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetSystemAdminFunctionTypesAsync().Returns(
            Task.FromResult<ServiceResult<LookupTypeCollection>>(
                new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([function]))));
        var model = new ReferenceQueryModel(api);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<ReferenceQueryModel>().Returns(model);
        var viewModel = new SystemAdminViewModel(appRoot);

        await viewModel.LoadFunctionTypesOperation.ExecuteAsync();

        viewModel.FunctionTypes.Should().Equal(function);
        viewModel.GetFunctionType(0).Should().BeSameAs(function);
        viewModel.GetFunctionType(1).Should().BeNull();
        await api.Received(1).GetSystemAdminFunctionTypesAsync();
    }
}
