using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

public sealed class NativeCheckpointEndpointGuardTests
{
    [Theory]
    [InlineData("Production",true,true)]
    [InlineData("Development",false,true)]
    [InlineData("Development",true,false)]
    public async Task Unavailable_or_unauthorized_checkpoint_never_resolves_or_starts_a_native_owner(string environment,bool enabled,bool token)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName=environment});builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["SessionCapture:Enabled"]=enabled.ToString(),["SessionCapture:AccessToken"]=new string('a',48)});
        // Deliberately no native owner/library registration: an unauthorized route must not resolve either.
        await using var app=builder.Build();app.MapNativeCheckpointBoundary();await app.StartAsync();
        var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client=new HttpClient{BaseAddress=new Uri(address)};
        if(token)client.DefaultRequestHeaders.Add("X-IFM-Session-Capture",new string('a',48));
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync("/internal/session-capture/native/checkpoint/export/1234/012345abcdef",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync("/internal/session-capture/native/checkpoint/import/012345abcdef",null)).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task Development_cannot_import_even_with_authentication_and_opt_in()
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"});builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["SessionCapture:Enabled"]="true",["SessionCapture:AllowCheckpointImport"]="true",["SessionCapture:AccessToken"]=new string('a',48)});
        await using var app=builder.Build();app.MapNativeCheckpointBoundary();await app.StartAsync();
        var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client=new HttpClient{BaseAddress=new Uri(address)};client.DefaultRequestHeaders.Add("X-IFM-Session-Capture",new string('a',48));
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync("/internal/session-capture/native/checkpoint/import/012345abcdef",null)).StatusCode);
        await app.StopAsync();
    }
}
