using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;


namespace TomasAI.IFM.Domain.MarketData.Securities.BDDTests;

public class FuturesOptionContractCommandHandlerTests
{
    [Fact]
    public void GivenExistingOption_WhenOverwriteAddIsRequested_ThenStateChangesWithoutFailure()
    {
        var state = new FuturesOptionContractCommandState();
        new AddFuturesOptionContractCommand(SampleData.FuturesOptionContract)
        {
            CommandId = Guid.NewGuid()
        }.Execute(state);
        var overwrite = new AddFuturesOptionContractCommand(
            SampleData.FuturesOptionContract,
            overwrite: true)
        {
            CommandId = Guid.NewGuid()
        };

        var changed = overwrite.Execute(state);

        changed.Should().BeTrue();
        state.Events.Should().HaveCount(2);
    }
}
