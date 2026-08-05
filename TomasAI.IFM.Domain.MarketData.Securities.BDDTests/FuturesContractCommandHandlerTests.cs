using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;

namespace TomasAI.IFM.Domain.MarketData.Securities.BDDTests;

public class FuturesContractCommandHandlerTests
{
    [Fact]
    public void GivenEmptyState_WhenContractIsAdded_ThenContractExistsAndEventIsRecorded()
    {
        var state = new FuturesContractCommandState();
        var command = new AddFuturesContractCommand(SampleData.FuturesContract)
        {
            CommandId = Guid.NewGuid()
        };

        var changed = command.Execute(state);

        changed.Should().BeTrue();
        state.FuturesContractExists(SampleData.FuturesContract.Id).Should().BeTrue();
        state.Events.Should().ContainSingle();
    }
}
