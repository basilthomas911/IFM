using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests.FuturesContract;

public sealed class InstrumentDefinitionQueryTests
{
    [Fact]
    public async Task Selection_query_uses_injected_domain_store_and_passes_cancellation()
    {
        var context=Substitute.For<IFuturesContractQueryContext>();
        var store=Substitute.For<IInstrumentDefinitionStore>();
        context.InstrumentDefinitions.Returns(store);
        using var deadline=new CancellationTokenSource();
        var request=new InstrumentDefinitionPageRequest {Root="ES",Options=true};
        var page=new InstrumentDefinitionPage(Guid.NewGuid(),DateTime.UtcNow,[],null);
        store.GetSelectionPageAsync(request,Arg.Any<DateTimeOffset>(),deadline.Token).Returns(page);
        var query=new GetInstrumentDefinitionsQuery {
            Request=request,
            Subject=new ActorSubject(ActorType.Query,GetInstrumentDefinitionsQuery.Actor,GetInstrumentDefinitionsQuery.Verb,"fixture")};
        await query.ExecuteAsync(context,deadline.Token);
        await store.Received(1).GetSelectionPageAsync(request,Arg.Any<DateTimeOffset>(),deadline.Token);
        _=context.DidNotReceive().DbFactory;
    }
}
