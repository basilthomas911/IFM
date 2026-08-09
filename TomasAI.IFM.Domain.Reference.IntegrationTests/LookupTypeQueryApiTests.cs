using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public class LookupTypeQueryApiTests(WebApplicationFactory<Program> factory, ReferenceFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<ReferenceFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetLookupTypesQuery_Ok()
    {
        // arrange...
        var lookupType = SampleData.LookupType1;
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetLookupTypesAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(e =>
            e.LookupTypeName == lookupType.LookupTypeName &&
            e.ShortCode == lookupType.ShortCode &&
            e.Description == lookupType.Description);
    }

    [Fact]
    public async Task GetLookupTypeQuery_Ok()
    {
        // arrange...
        var lookupType1 = SampleData.LookupType1;
        var lookupType2 = SampleData.LookupType2;
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType1.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType2.Id);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType1);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType2);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetLookupTypesAsync(lookupType1.LookupTypeName);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(e =>
            e.LookupTypeName == lookupType1.LookupTypeName &&
            e.ShortCode == lookupType1.ShortCode);
        response.Value.Should().Contain(e =>
            e.LookupTypeName == lookupType2.LookupTypeName &&
            e.ShortCode == lookupType2.ShortCode);
    }

    [Fact]
    public async Task GetLookupTypeNamesQuery_Ok()
    {
        // arrange...
        var lookupType1 = SampleData.LookupType1;
        var lookupType3 = SampleData.LookupType3;
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType1.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType3.Id);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType1);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType3);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetLookupTypeNamesAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(lookupType1.LookupTypeName);
        response.Value.Should().Contain(lookupType3.LookupTypeName);
    }

    [Fact]
    public async Task GetLookupTypeShortCodesQuery_Ok()
    {
        // arrange...
        var lookupType1 = SampleData.LookupType1;
        var lookupType2 = SampleData.LookupType2;
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType1.Id);
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType2.Id);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType1);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType2);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.GetLookupTypeShortCodesAsync(lookupType1.LookupTypeName);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(e => e.ShortCode == lookupType1.ShortCode);
        response.Value.Should().Contain(e => e.ShortCode == lookupType2.ShortCode);
    }

    [Fact]
    public async Task LookupTypeShortCodeExistsQuery_ReturnsTrue()
    {
        // arrange...
        var lookupType = SampleData.LookupType1;
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.LookupTypeShortCodeExistsAsync(lookupType.LookupTypeName, lookupType.ShortCode);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().BeTrue();
    }

    [Fact]
    public async Task LookupTypeShortCodeExistsQuery_ReturnsFalse()
    {
        // arrange...
        var lookupType = SampleData.LookupType1;
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);
        await dbFixture.ReferenceDb.InsertLookupTypeAsync(lookupType);

        // act...
        var referenceApi = new ReferenceQueryApi(_actorProducer);
        var response = await referenceApi.LookupTypeShortCodeExistsAsync(lookupType.LookupTypeName, "NONEXISTENT");

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().BeFalse();
    }

}
