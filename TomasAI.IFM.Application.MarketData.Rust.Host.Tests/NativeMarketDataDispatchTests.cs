using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using Xunit.Abstractions;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

public sealed class NativeMarketDataDispatchTests(ITestOutputHelper output)
{
    [Fact(Timeout = 15000)]
    public async Task All_declared_async_operations_keep_their_wire_shape_on_the_shared_dispatcher()
    {
        var backend = DispatchProxy.Create<IMarketDataApi, ShapeBackend>();
        await RunHostAsync(backend, async (connection, token) =>
        {
            foreach (var method in typeof(IMarketDataApi).GetMethods().Where(method => typeof(Task).IsAssignableFrom(method.ReturnType)))
            {
                var arguments = method.GetParameters().Select(static _ => Array.Empty<byte>()).ToArray();
                var call = new MarketDataApiCall(MarketDataApiClient.Signature(method), arguments);
                var reply = await connection.PostAsync<object?>("api/" + method.Name, call, token);
                if (method.ReturnType == typeof(Task))
                    Assert.Equal(true, reply);
            }
        });
    }

    [Fact(Timeout = 15000)]
    public async Task Concurrent_lanes_and_operation_retries_share_the_dispatcher_without_crossing_arguments()
    {
        var backend = Substitute.For<IMarketDataApi>();
        var date = new DateOnly(2026, 11, 6);
        var id = Guid.NewGuid();
        backend.GetBackendStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new MarketDataBackendStatus(true, date, []));
        backend.GetFuturesContractAsync("ES").Returns(new FuturesContractV3ReadModel { ContractId = "ES" });
        backend.StopAsync(date).Returns(Task.CompletedTask);
        await RunHostAsync(backend, async (connection, token) =>
        {
            var client = new MarketDataApiClient(connection, backend);
            var original = Call(nameof(IMarketDataApi.GetFuturesContractAsync), MarketDataHostConnection.Encode("ES"));
            var first = connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesContractAsync", original, token, id);
            var retry = connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesContractAsync", original, token, id);
            var status = client.GetBackendStatusAsync(token);
            var stop = connection.PostAsync<bool>("api/StopAsync", Call(nameof(IMarketDataApi.StopAsync), MarketDataHostConnection.Encode(date)), token);
            await Task.WhenAll(first, retry, status, stop);
            Assert.Equal("ES", (await first).ContractId);
            Assert.Equal("ES", (await retry).ContractId);
            Assert.Equal(date, (await status).ValueDate);
            Assert.True(await stop);

            var conflict = original with { Arguments = [MarketDataHostConnection.Encode("VX")] };
            var conflictingRequest = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesContractAsync", conflict, token, id));
            Assert.Contains("reused with different input", conflictingRequest.Message, StringComparison.Ordinal);
            var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesPriceAsync", original, token));
            Assert.Contains("signature differs from its envelope", mismatch.Message, StringComparison.Ordinal);
            var missingSignature = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesContractAsync", original with { Signature = null! }, token));
            Assert.Contains("signature differs from its envelope", missingSignature.Message, StringComparison.Ordinal);
        });
        await backend.Received(1).GetFuturesContractAsync("ES");
        await backend.DidNotReceive().GetFuturesContractAsync("VX");
        await backend.Received(1).GetBackendStatusAsync(Arg.Is<CancellationToken>(token => token.CanBeCanceled));
        await backend.Received(1).StopAsync(date);
    }

    [Fact(Timeout = 15000)]
    public async Task Full_memo_reuses_completed_capacity_while_stop_bypasses_busy_control()
    {
        var backend = Substitute.For<IMarketDataApi>();
        var date = new DateOnly(2026, 11, 6);
        var wait = new TaskCompletionSource<FuturesContractV3ReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var busy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        backend.GetFuturesContractAsync("ES").Returns(new FuturesContractV3ReadModel { ContractId = "ES" });
        backend.GetFuturesContractAsync("wait").Returns(_ =>
        {
            if (Interlocked.Increment(ref entered) == 8)
                busy.TrySetResult();
            return wait.Task;
        });
        backend.StopAsync(date).Returns(Task.CompletedTask);
        await RunHostAsync(backend, async (connection, token) =>
        {
            var call = Call(nameof(IMarketDataApi.GetFuturesContractAsync), MarketDataHostConnection.Encode("ES"));
            var first = Guid.NewGuid();
            _ = await connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesContractAsync", call, token, first);
            await Parallel.ForAsync(0, 1024, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = 8 },
                async (_, ct) =>
                {
                    await connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesContractAsync", call, ct);
                });
            _ = await connection.PostAsync<FuturesContractV3ReadModel>("api/GetFuturesContractAsync", call, token, first);

            var blocked = call with { Arguments = [MarketDataHostConnection.Encode("wait")] };
            var jobs = Enumerable.Range(0, 8)
                .Select(_ => connection.PostAsync<FuturesContractV3ReadModel?>("api/GetFuturesContractAsync", blocked, token))
                .ToArray();
            try
            {
                await busy.Task.WaitAsync(token);
                Assert.True(await connection.PostAsync<bool>("api/StopAsync",
                    Call(nameof(IMarketDataApi.StopAsync), MarketDataHostConnection.Encode(date)), token));
            }
            finally
            {
                wait.TrySetResult(null);
                await Task.WhenAll(jobs);
            }
        });
        await backend.Received(1026).GetFuturesContractAsync("ES");
        await backend.Received(1).StopAsync(date);
    }

    [Fact(Timeout = 15000)]
    public async Task Bulk_history_backlog_leaves_startup_and_emergency_operations_available()
    {
        var expectedIsolation = Environment.GetEnvironmentVariable("IFM_EXPECT_BULK_ISOLATION") != "0";
        var backend = Substitute.For<IMarketDataApi>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 6);
        var request = new MarketDataHistoricalRequest
        {
            DataLoadAttemptId = id, Series = [], StartDate = date, EndDate = date,
            MaximumCostUsd = 0, MaximumBytes = 1, NormalizationVersion = "test", RequestedBy = "test"
        };
        backend.LoadHistoricalAsync(Arg.Any<MarketDataHistoricalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(call.Arg<CancellationToken>());
                return new HistoricalDataLoaderState
                {
                    DataLoadAttemptId = id, RequestSha256 = "test",
                    Checkpoint = new HistoricalAcquisitionCheckpoint { DataLoadAttemptId = id }
                };
            });
        backend.ReadIndicatorSeedBarsAsync("ES", TimeFrameType.OneMinute, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FuturesTradeSessionBarReadModel>());
        backend.StopAsync(date).Returns(Task.CompletedTask);

        await RunHostAsync(backend, async (connection, token) =>
        {
            const string path = "api/LoadHistoricalAsync";
            var call = Call(nameof(IMarketDataApi.LoadHistoricalAsync), MarketDataHostConnection.Encode(request), []);
            var first = connection.PostAsync<HistoricalDataLoaderState>(path, call, token, id);
            await entered.Task.WaitAsync(token);
            var duplicates = Enumerable.Range(0, 12).Select(_ => ObserveRetryAsync()).ToArray();
            try
            {
                // Let the original control subscriber admit the backlog before
                // measuring an otherwise trivial seed request.
                await Task.Delay(50, token);
                var watch = Stopwatch.StartNew();
                using var probe = CancellationTokenSource.CreateLinkedTokenSource(token);
                probe.CancelAfter(TimeSpan.FromMilliseconds(250));
                var seed = connection.PostAsync<FuturesTradeSessionBarReadModel[]>(
                    "api/ReadIndicatorSeedBarsAsync", Call(nameof(IMarketDataApi.ReadIndicatorSeedBarsAsync),
                        MarketDataHostConnection.Encode("ES"), MarketDataHostConnection.Encode(TimeFrameType.OneMinute), []), probe.Token);
                if (expectedIsolation)
                    Assert.Empty(await seed);
                else
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => seed);
                output.WriteLine($"BULK_PROBE ms={watch.Elapsed.TotalMilliseconds:F3} isolated={expectedIsolation}");

                Assert.False(first.IsCompleted);
                Assert.True(await connection.PostAsync<bool>("api/StopAsync",
                    Call(nameof(IMarketDataApi.StopAsync), MarketDataHostConnection.Encode(date)), token));
            }
            finally
            {
                release.TrySetResult();
                await Task.WhenAll(duplicates);
                Assert.Equal(id, (await first).DataLoadAttemptId);
            }
            Assert.Equal(id, (await connection.PostAsync<HistoricalDataLoaderState>(path, call, token, id)).DataLoadAttemptId);
            if (expectedIsolation) Assert.Contains(duplicates, retry => !retry.Result);

            async Task<bool> ObserveRetryAsync()
            {
                try
                {
                    var result = await connection.PostAsync<HistoricalDataLoaderState>(path, call, token, id);
                    Assert.Equal(id, result.DataLoadAttemptId);
                    return true;
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains("bulk history capacity exhausted", StringComparison.Ordinal))
                {
                    return false;
                }
            }
        });
        await backend.Received(1).LoadHistoricalAsync(Arg.Any<MarketDataHistoricalRequest>(), Arg.Any<CancellationToken>());
    }

    private static MarketDataApiCall Call(string name, params byte[][] arguments)
        => new(MarketDataApiClient.Signature(typeof(IMarketDataApi).GetMethod(name)!), arguments);

    private static async Task RunHostAsync(IMarketDataApi backend, Func<MarketDataHostConnection, CancellationToken, Task> run)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var url = Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_NATS_URL") ?? "nats://127.0.0.1:4222";
        await using var connections = new NatsConnectionManager();
        await using var connection = new MarketDataHostConnection(connections, url, "dispatchtest" + Guid.NewGuid().ToString("N"));
        using var service = new NativeMarketDataNatsService(backend, connection, NullLogger<NativeMarketDataNatsService>.Instance, TimeProvider.System);
        await service.StartAsync(deadline.Token);
        try
        {
            await service.Ready.WaitAsync(deadline.Token);
            var client = await connection.GetClientAsync(deadline.Token);
            await client.PingAsync(deadline.Token);
            await run(connection, deadline.Token);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    public class ShapeBackend : DispatchProxy
    {
        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var result = targetMethod!.ReturnType;
            if (result == typeof(Task))
                return Task.CompletedTask;
            var resultType = result.GetGenericArguments()[0];
            var value = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [value])!;
        }
    }
}
