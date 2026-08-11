using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Presentation.UnitTests.Models;

public class ReferenceQueryModelTests
{
    [Fact]
    public async Task LoadReferenceDataDefinitionTypesAsync_ReportsServiceFailureAndDoesNotPublishData()
    {
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetReferenceDataDefinitionTypesAsync().Returns(
            Task.FromResult<ServiceResult<LookupTypeCollection>>(
                new ServiceFailed<LookupTypeCollection>(741, "reference unavailable")));
        var model = new ReferenceQueryModel(api);
        var published = false;
        var errorCode = 0;
        var errorMessage = string.Empty;
        model.OnError((code, message) => (errorCode, errorMessage) = (code, message));

        await model.LoadReferenceDataDefinitionTypesAsync(_ => published = true);

        published.Should().BeFalse();
        errorCode.Should().Be(741);
        errorMessage.Should().Be("reference unavailable");
    }
}
