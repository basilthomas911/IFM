using NSubstitute;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;
public sealed class DesktopTradeExecutionContextTests
{
    [Fact] public void Development_defaults_to_emulator() => Assert.Equal(new DesktopTradeExecutionContext("IFM-EMULATOR-PAPER", BrokerEnvironment.Emulator), DesktopTradeExecutionContext.Resolve(null,"Development"));
    [Fact] public void Production_requires_explicit_route() => Assert.Throws<InvalidOperationException>(()=>DesktopTradeExecutionContext.Resolve(null,"Production"));
    [Theory] [InlineData("Emulator")] [InlineData("Paper")] [InlineData("Live")]
    public void Explicit_route_preserves_account_and_environment(string mode)
    {
        var configuration=Substitute.For<IConfiguration>(); configuration["TradeBroker:AccountAlias"].Returns("SelectedAccount"); configuration["TradeBroker:Environment"].Returns(mode);
        var route=DesktopTradeExecutionContext.Resolve(configuration,"Production");
        Assert.Equal("SelectedAccount",route.AccountAlias);Assert.Equal(mode,route.Environment.ToString());
    }
    [Theory] [InlineData("Unknown")] [InlineData("Live")] [InlineData("Paper")]
    public void Development_does_not_allow_live_or_unknown_route(string mode)
    {
        var configuration=Substitute.For<IConfiguration>(); configuration["TradeBroker:Environment"].Returns(mode);
        Assert.Throws<InvalidOperationException>(()=>DesktopTradeExecutionContext.Resolve(configuration,"Development"));
    }
}
