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

public sealed class NativeProductionPauseEndpointGuardTests
{
    [Theory]
    [InlineData("Production",true,true)]
    [InlineData("Development",false,true)]
    [InlineData("Development",true,false)]
    public async Task Disabled_or_unauthorized_capture_controls_never_resolve_a_native_owner(string environment,bool enabled,bool authenticated)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName=environment});builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["SessionCapture:Enabled"]=enabled.ToString(),["SessionCapture:AccessToken"]=new string('a',48)});
        // No owner/library is registered: authorization precedes every owner resolution.
        await using var app=builder.Build();app.MapNativeSessionCaptureBoundary();await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
        if(authenticated)client.DefaultRequestHeaders.Add("X-IFM-Session-Capture",new string('a',48));
        foreach(var route in new[]{"pause-production/1234","resume-production/1234","hold/1234","release/1234"})
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync("/internal/session-capture/native/"+route,null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/internal/session-capture/native/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/internal/session-capture/native/prefix/1234")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync("/internal/session-capture/native/stop-providers",null)).StatusCode);
        await app.StopAsync();
    }
    [Fact]
    public async Task Invalid_pause_lease_is_refused_without_loading_native_library()
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"});builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["SessionCapture:Enabled"]="true",["SessionCapture:AccessToken"]=new string('a',48)});
        await using var app=builder.Build();app.MapNativeSessionCaptureBoundary();await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};client.DefaultRequestHeaders.Add("X-IFM-Session-Capture",new string('a',48));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync("/internal/session-capture/native/pause-production/0",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync("/internal/session-capture/native/resume-production/nothex",null)).StatusCode);
        await app.StopAsync();
    }
}
