using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventChannel;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventChannel;

public class ConcurrentAsyncEventChannelTests
{
    readonly ILogger<Shared.EventChannel.EventChannel>? _logger = default!;

    [Fact]
    public void Constructor_Sync_InitializesCorrectly()
    {
        // Arrange
        var channelName = "test-sync-channel";
        var processedMessages = new List<string>();

        // Act
        var channel = new ConcurrentAsyncEventChannel<string>(channelName, reader, _logger);

        // Assert
        Assert.NotNull(channel);

        ValueTask reader(string message) 
        { 
            processedMessages.Add(message); 
            return ValueTask.CompletedTask; 
        }

    }

    [Fact]
    public void StartAndStop_Sync_ProcessesMessages()
    {
        // Arrange
        var channelName = "test-sync-process";
        var processedMessages = new List<string>();
        var mre = new ManualResetEvent(false);
        var channel = new ConcurrentAsyncEventChannel<string>(channelName, ReaderAsync, _logger);

        // Act
        channel.Start();
        channel.WriteData("message1");
        channel.WriteData("message2");

        // Assert
        Assert.True(mre.WaitOne(TimeSpan.FromSeconds(5)), "Messages were not processed in time.");
        channel.Stop();
        Assert.Equal(2, processedMessages.Count);
        Assert.Equal("message1", processedMessages[0]);
        Assert.Equal("message2", processedMessages[1]);
        Assert.False(channel.IsOpen);

        async ValueTask ReaderAsync(string message)
        {
            processedMessages.Add(message);
            if (processedMessages.Count == 2)
            {
                mre.Set();
            }
            await ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_Async_InitializesCorrectly()
    {
        // Arrange
        var channelName = "test-async-channel";
        var processedMessages = new List<string>();
       

        // Act
        var channel = new ConcurrentAsyncEventChannel<string>(channelName, ReaderAsync, _logger);

        // Assert
        Assert.NotNull(channel);

        async ValueTask ReaderAsync(string message)
        {
            processedMessages.Add(message);
            await ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task StartAndStop_Async_ProcessesMessages()
    {
        // Arrange
        var channelName = "test-async-process";
        var processedMessages = new List<string>();
        var tcs = new TaskCompletionSource<bool>();
        var channel = new ConcurrentAsyncEventChannel<string>(channelName, ReaderAsync, _logger);

        // Act
        channel.Start();
        channel.WriteData("message1");
        channel.WriteData("message2");
        await Task.Delay(1000); // Allow time for the messages to be processed
        var result = await tcs.Task;
        channel.Stop();

        // Assert
        Assert.True(result);
        Assert.Equal(2, processedMessages.Count);
        Assert.Equal("message1", processedMessages[0]);
        Assert.Equal("message2", processedMessages[1]);
        Assert.False(channel.IsOpen);

        async ValueTask ReaderAsync(string message)
        {
            processedMessages.Add(message);
            if (processedMessages.Count == 2)
            {
                tcs.SetResult(true);
            }
            await ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void IsOpen_ReturnsFalse_WhenChannelIsNull()
    {
        // Arrange
        var channelName = "test-empty-channel";
        var channel = new ConcurrentAsyncEventChannel<string>(channelName, reader, _logger);

        // Assert
        Assert.False(channel.IsOpen);

        static ValueTask reader(string _) 
            => ValueTask.CompletedTask;
    }

    [Fact]
    public void IsOpen_ReturnsTrue_WhenChannelIsStartedButHasNoItems()
    {
        // Arrange
        var channelName = "test-empty-started-channel";
         var channel = new ConcurrentAsyncEventChannel<string>(channelName, ReaderAsync, _logger);

        // Act
        channel.Start();

        // Assert
        Assert.True(channel.IsOpen);
        channel.Stop();

        static ValueTask ReaderAsync(string _)
            => ValueTask.CompletedTask;

    }

    [Fact]
    public async Task IsOpen_ReturnsTrue_WhenChannelHasItems()
    {
        // Arrange
        var channelName = "test-not-empty-channel";
        var tcs = new TaskCompletionSource<bool>();
        var channel = new ConcurrentAsyncEventChannel<string>(channelName, ReaderAsync, _logger);

        // Act
        channel.Start();
        channel.WriteData("some data");

        // Assert
        Assert.True(channel.IsOpen);
        await Task.Delay(1); // Allow time for the message to be processed
        channel.Stop();
        Assert.False(channel.IsOpen);

        ValueTask ReaderAsync(string _)
        {
            tcs.SetResult(true);
            return ValueTask.CompletedTask;
        }
    }
}
