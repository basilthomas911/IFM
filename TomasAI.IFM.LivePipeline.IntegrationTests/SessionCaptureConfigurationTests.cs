using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class SessionCaptureConfigurationTests
{
    [Fact]
    public void Actual_flat_configuration_excludes_credentials_and_identifies_every_effective_owner_path()
    {
        var values=new Dictionary<string,string?>{["ConnectionStrings:TradeDbConnection"]="Host=127.0.0.1;Database=owned;Username=hidden;Password=hidden",
            ["ConnectionStrings:TradeTickQuoteDbConnection"]="Contact Points=127.0.0.1;Default Keyspace=owned",["AppSettings:Fmp:ApiKey"]="hidden",["SessionCapture:AccessToken"]="hidden",
            ["TradeBroker:Emulator:AccountAlias"]="authentic-account",["Actors:RealtimeMailboxes:Enabled"]="false"};
        var config=new ConfigurationBuilder().AddInMemoryCollection(values).Build();var evidence=SessionCaptureConfiguration.Export(config);
        Assert.False(evidence.AllOwnerPathsExplicit);Assert.Equal(HistoricalSessionReplay.OwnerPathKeys.Length,evidence.OwnerPaths.Count);
        Assert.DoesNotContain("hidden",string.Join(";",evidence.Settings.Values));Assert.Equal("authentic-account",evidence.Settings["TradeBroker:Emulator:AccountAlias"]);
        foreach(var path in evidence.OwnerPaths){Assert.True(Path.IsPathFullyQualified(path.Value));values[path.Key]=path.Value;}
        Assert.True(SessionCaptureConfiguration.Export(new ConfigurationBuilder().AddInMemoryCollection(values).Build()).AllOwnerPathsExplicit);
    }
}
