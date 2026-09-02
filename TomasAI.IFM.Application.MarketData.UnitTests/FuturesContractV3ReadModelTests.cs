using FluentAssertions;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class FuturesContractV3ReadModelTests
{
    static readonly FuturesContractV3ReadModel VxBack = new(
        "VX20261021", "VX October 2026", "VX", "VXV6", "FUT", "USD",
        "CFE", "1000", new DateOnly(2026, 10, 21), false, true);

    [Fact]
    public void OnTheRunWithoutRolloverIsInvalid()
    {
        var invalid = VxBack with { OnTheRun = true, Rollover = false };

        invalid.IsValid.Should().BeFalse();
        new FuturesContractValidationRules().Execute(invalid)
            .Should().Contain(error => error.ErrorMessage.Contains(
                "rollover set", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VxBackCanBelongToRolloverSetWithoutBeingOnTheRun()
    {
        VxBack.IsValid.Should().BeTrue();
        VxBack.OnTheRun.Should().BeFalse();
        VxBack.Rollover.Should().BeTrue();
    }

    [Fact]
    public void MessagePackRoundTripPreservesBothOperationalFlags()
    {
        var actual = MessagePackSerializer.Deserialize<FuturesContractV3ReadModel>(
            MessagePackSerializer.Serialize(VxBack));

        actual.Should().Be(VxBack);
    }

    [Fact]
    public void JsonRoundTripPreservesBothOperationalFlags()
    {
        var actual = JsonConvert.DeserializeObject<FuturesContractV3ReadModel>(
            JsonConvert.SerializeObject(VxBack));

        actual.Should().Be(VxBack);
    }
}
