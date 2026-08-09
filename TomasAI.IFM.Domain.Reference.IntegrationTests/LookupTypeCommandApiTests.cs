using TomasAI.IFM.Domain.Reference.Shared.CommandParameters;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public class LookupTypeCommandApiTests(WebApplicationFactory<Program> factory, ReferenceFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<ReferenceFixture>
{
    static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(10);
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task AddLookupType_Ok()
    {
        // arrange...
        var lookupType = SampleData.LookupType1;
        var cmdParam = new AddLookupTypeParameter(lookupType, AddLookupTypeCommand.ErrorId);
        var entityId = new LookupTypeId(cmdParam.LookupType.LookupTypeName, cmdParam.LookupType.OrderId);
        var subject = new ActorSubject(ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, entityId.Format());
        await ClearEventStreamAsync(subject);

        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);

        // act...
        var referenceApi = new ReferenceCommandApi(_actorProducer);
        var response = await referenceApi.AddLookupTypeAsync(lookupType);

        await WaitUntilAsync(async () => await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id) is not null);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
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
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, entityId.Format()));
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, ChangeLookupTypeCommand.Actor, ChangeLookupTypeCommand.Verb, entityId.Format()));

        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);

        // first add the lookup type
        var referenceApi = new ReferenceCommandApi(_actorProducer);
        var addResponse = await referenceApi.AddLookupTypeAsync(lookupType);

        await WaitUntilAsync(async () => await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id) is not null);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue(addResponse.ErrorMessage);

        // create changed lookup type with updated description
        var changedLookupType = lookupType with { Description = "Updated United States Dollar Description" };

        // act...
        var changeResponse = await referenceApi.ChangeLookupTypeAsync(lookupTypeId, changedLookupType, overwrite: true);

        await WaitUntilAsync(async () =>
            (await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id))?.Description == changedLookupType.Description);

        // assert...
        changeResponse.Should().NotBeNull();
        changeResponse.Success.Should().BeTrue(changeResponse.ErrorMessage);
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
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, lookupTypeId.Format()));
        await ClearEventStreamAsync(new ActorSubject(
            ActorType.Command, RemoveLookupTypeCommand.Actor, RemoveLookupTypeCommand.Verb, lookupTypeId.Format()));

        // first ensure the lookup type exists by adding it
        await dbFixture.ReferenceDb.DeleteLookupTypeAsync(lookupType.Id);

        var referenceApi = new ReferenceCommandApi(_actorProducer);
        var addResponse = await referenceApi.AddLookupTypeAsync(lookupType);

        await WaitUntilAsync(async () => await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id) is not null);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue(addResponse.ErrorMessage);

        // verify lookup type exists before removal
        var existingLookupType = await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id);
        existingLookupType.Should().NotBeNull();

        // act...
        var removeResponse = await referenceApi.RemoveLookupTypeAsync(lookupTypeId, overwrite: true);

        await WaitUntilAsync(async () => await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id) is null);

        // assert...
        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue(removeResponse.ErrorMessage);
        removeResponse.Value.Should().NotBe(Guid.Empty);

        // verify lookup type was removed from database
        var removedLookupType = await dbFixture.ReferenceDb.GetLookupTypeAsync(lookupType.Id);
        removedLookupType.Should().BeNull();
    }

    async Task ClearEventStreamAsync(ActorSubject subject)
    {
        dbFixture.BlackboardService.EventSourcing.EventStreamId.Remove($"{subject.ThreadId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
    }

    static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(StateTimeout);
        while (!await condition())
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
    }
}
