using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>Serializes composition-root verification because Startup owns static application state.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StartupCompositionCollection
{
    /// <summary>Gets the collection name.</summary>
    public const string Name = "UI startup composition";
}

/// <summary>Verifies the complete typed service catalog through the real Simple Injector composition root.</summary>
[Collection(StartupCompositionCollection.Name)]
[Trait("Category", "G0Infrastructure")]
public sealed class StartupCompositionTests
{
    /// <summary>Ensures all UIR services and forms verify without opening a window or connecting to NATS.</summary>
    [Fact]
    public async Task Configure_VerifiesTypedUiServices_WithoutGenericModelResolution()
    {
        Exception? failure = null;
        object? navigator = null;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AppSettings:AppEnvironment"] = "Development",
                        ["AppSettings:NatsServerUri"] = "nats://127.0.0.1:4222",
                        ["AppSettings:NatsStartupTimeoutSeconds"] = "1"
                    })
                    .Build();
                navigator = global::TomasAI.IFM.UI.Net.Startup.Configure(configuration);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completed.TrySetResult();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            failure.Should().BeNull();
            navigator.Should().NotBeNull();
        }
        finally
        {
            await global::TomasAI.IFM.UI.Net.Startup.ShutdownAsync();
        }
    }
}
