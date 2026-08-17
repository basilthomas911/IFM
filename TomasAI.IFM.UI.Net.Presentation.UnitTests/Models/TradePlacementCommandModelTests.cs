using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public sealed class TradePlacementCommandModelTests
{
    [Fact]
    public async Task Start_returns_confirmed_command_id_and_forwards_cancellation()
    {
        var api = Substitute.For<ITradePlacementCommandApi>();
        var expected = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        api.StartTradePlacementAsync(Arg.Any<TradePlacementId>(), cancellation.Token)
            .Returns(new ServiceResult<Guid> { Success = true, Value = expected });
        var subject = new TradePlacementCommandModel(api);

        var actual = await subject.StartTradePlacementAsync(
            "ES20260918",
            new DateOnly(2026, 8, 17),
            cancellation.Token);

        actual.Should().Be(expected);
        await api.Received(1).StartTradePlacementAsync(
            Arg.Is<TradePlacementId>(id => id.ContractId == "ES20260918"
                && id.ValueDate == new DateOnly(2026, 8, 17)),
            cancellation.Token);
    }

    [Fact]
    public async Task Stop_returns_empty_id_when_terminal_command_is_not_accepted()
    {
        var api = Substitute.For<ITradePlacementCommandApi>();
        using var cancellation = new CancellationTokenSource();
        api.StopTradePlacementAsync(Arg.Any<TradePlacementId>(), cancellation.Token)
            .Returns(new ServiceResult<Guid> { Success = false, ErrorMessage = "unavailable" });
        var subject = new TradePlacementCommandModel(api);

        var actual = await subject.StopTradePlacementAsync(
            "ES20260918",
            new DateOnly(2026, 8, 17),
            cancellation.Token);

        actual.Should().Be(Guid.Empty);
        await api.Received(1).StopTradePlacementAsync(
            Arg.Any<TradePlacementId>(),
            cancellation.Token);
    }
}
