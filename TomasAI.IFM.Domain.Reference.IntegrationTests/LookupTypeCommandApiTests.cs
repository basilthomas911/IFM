using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.CommandParameters;
using TomasAI.IFM.Shared.Reference.Commands;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public class LookupTypeCommandApiTests(WebApplicationFactory<Program> factory, ReferenceFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<ReferenceFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task AddLookupType_Ok()
    {
        // arrange...
        var lookupType = SampleData.LookupType1;
        var cmdParam = new AddLookupTypeParameter(lookupType, AddLookupTypeCommand.ErrorId);
        var entityId = new LookupTypeId(cmdParam.LookupType.LookupTypeName, cmdParam.LookupType.OrderId);
        var subject = new ActorSubject(ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceCommandApi(commandServiceApi);
        var response = await referenceApi.AddLookupTypeAsync(lookupType);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);

        // verify lookup type was added to database
        var savedLookupType = await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id);
        savedLookupType.Should().NotBeNull();
        savedLookupType!.LookupTypeName.Should().Be(lookupType.LookupTypeName);
        savedLookupType.ShortCode.Should().Be(lookupType.ShortCode);
        savedLookupType.OrderId.Should().Be(lookupType.OrderId);
        savedLookupType.Description.Should().Be(lookupType.Description);
    }

    [Fact]
    public async Task ChangeLookupType_Ok()
    {
        // arrange...
        var lookupType = SampleData.LookupType1;
        var lookupTypeId = new LookupTypeId(lookupType.LookupTypeName, lookupType.OrderId);
        var cmdParam = new AddLookupTypeParameter(lookupType, AddLookupTypeCommand.ErrorId);
        var entityId = new LookupTypeId(cmdParam.LookupType.LookupTypeName, cmdParam.LookupType.OrderId);
        var subject = new ActorSubject(ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);

        // first add the lookup type
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceCommandApi(commandServiceApi);
        var addResponse = await referenceApi.AddLookupTypeAsync(lookupType);

        // wait for denormalization to complete
        await Task.Delay(1000);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue();

        // create changed lookup type with updated description
        var changedLookupType = lookupType with { Description = "Updated United States Dollar Description" };

        // act...
        var changeResponse = await referenceApi.ChangeLookupTypeAsync(lookupTypeId, changedLookupType, overwrite: true);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // assert...
        changeResponse.Should().NotBeNull();
        changeResponse.Success.Should().BeTrue();
        changeResponse.Value.Should().NotBe(Guid.Empty);

        // verify lookup type was changed in database
        var savedLookupType = await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id);
        savedLookupType.Should().NotBeNull();
        savedLookupType!.LookupTypeName.Should().Be(changedLookupType.LookupTypeName);
        savedLookupType.ShortCode.Should().Be(changedLookupType.ShortCode);
        savedLookupType.OrderId.Should().Be(changedLookupType.OrderId);
        savedLookupType.Description.Should().Be(changedLookupType.Description);
    }

    [Fact]
    public async Task RemoveLookupType_Ok()
    {
        // arrange...
        var lookupType = SampleData.LookupType1;
        var lookupTypeId = new LookupTypeId(lookupType.LookupTypeName, lookupType.OrderId);

        // first ensure the lookup type exists by adding it
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);

        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var referenceApi = new ReferenceCommandApi(commandServiceApi);
        var addResponse = await referenceApi.AddLookupTypeAsync(lookupType);

        // wait for denormalization to complete
        await Task.Delay(1000);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue();

        // verify lookup type exists before removal
        var existingLookupType = await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id);
        existingLookupType.Should().NotBeNull();

        // act...
        var removeResponse = await referenceApi.RemoveLookupTypeAsync(lookupTypeId, overwrite: true);

        // wait for denormalization to complete
        await Task.Delay(1000);

        // assert...
        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue();
        removeResponse.Value.Should().NotBe(Guid.Empty);

        // verify lookup type was removed from database
        var removedLookupType = await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id);
        removedLookupType.Should().BeNull();
    }
}
