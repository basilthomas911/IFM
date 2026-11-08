using System.Buffers;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataApiPerformanceContractTests
{
    [Fact]
    public void Every_declared_method_and_overload_retains_its_exact_signature()
    {
        var catalog = new MarketDataOperationCatalog();
        foreach (var method in typeof(IMarketDataApi).GetMethods())
        {
            var types = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var resolved = catalog.Resolve(method.Name, types);
            Assert.Equal(method, resolved.Method);
            Assert.Equal(MarketDataApiClient.Signature(method), resolved.Signature);
            Assert.Same(resolved, catalog.Resolve(method.Name, types));
        }
        Assert.Throws<MissingMethodException>(() => catalog.Resolve("Unknown", []));
        Assert.Throws<MissingMethodException>(() => catalog.Resolve(nameof(IMarketDataApi.GetBackendStatusAsync), [typeof(string)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(218)]
    [InlineData(3640)]
    [InlineData(50000)]
    public void Payload_is_byte_for_byte_compatible_and_rentals_are_released(int count)
    {
        var pool = new TrackingPool();
        Type[] types = [typeof(string[])];
        object?[] arguments = [Enumerable.Range(0, count).Select(index => $"ES20261218-{index:D6}").ToArray()];
        var before = Before(nameof(IMarketDataApi.GetFuturesContractsAsync), types, arguments);
        var after = MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetFuturesContractsAsync), types, arguments, pool: pool);
        Assert.Equal(before, after);
        Assert.Equal(0, pool.Outstanding);
        Assert.True(pool.AllReturnedBytesWereCleared);
        pool.PoisonReturnedBuffers();
        Assert.Equal(before, after); // Transport retains independent bytes after pool reuse.
    }

    [Fact]
    public void Every_async_operation_keeps_its_wire_shape_with_value_null_and_cancellation_arguments()
    {
        foreach (var method in typeof(IMarketDataApi).GetMethods().Where(method => typeof(Task).IsAssignableFrom(method.ReturnType)))
        {
            var types = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var arguments = types.Select(type => type == typeof(CancellationToken) ? null
                : type == typeof(string) ? (object)"ES20261218"
                : type.IsValueType ? Activator.CreateInstance(type) : null).ToArray();
            Assert.Equal(Before(method.Name, types, arguments), MarketDataApiCallCodec.Encode(method.Name, types, arguments));
        }
    }

    [Fact]
    public async Task Existing_dispatcher_accepts_new_and_old_frames_and_retains_cancellation_parameters()
    {
        var backend = Substitute.For<IMarketDataApi>();
        backend.GetFuturesPriceAsync("ES20261218").Returns(Task.FromResult<decimal?>(5789.25m));
        Type[] types = [typeof(string)];
        object?[] arguments = ["ES20261218"];
        foreach (var bytes in new[] { Before(nameof(IMarketDataApi.GetFuturesPriceAsync), types, arguments),
            MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetFuturesPriceAsync), types, arguments) })
        {
            var call = MarketDataHostConnection.Decode<MarketDataApiCall>(bytes);
            var reply = await new MarketDataApiDispatcher(backend).InvokeAsync(call, CancellationToken.None);
            Assert.Equal(5789.25m, MarketDataHostConnection.Decode<decimal?>(reply));
        }
        var status = MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetBackendStatusAsync), [typeof(CancellationToken)], [null]);
        Assert.Equal(Before(nameof(IMarketDataApi.GetBackendStatusAsync), [typeof(CancellationToken)], [null]), status);
    }

    [Fact]
    public async Task Concurrent_first_access_and_payloads_do_not_share_mutable_buffers()
    {
        var catalog = new MarketDataOperationCatalog();
        await Task.WhenAll(Enumerable.Range(0, 128).Select(index => Task.Run(() =>
        {
            Type[] types = [typeof(string[])];
            object?[] args = [new[] { $"ES-{index}" }];
            Assert.Equal(MarketDataApiClient.Signature(typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.GetFuturesContractsAsync), types)!),
                catalog.Resolve(nameof(IMarketDataApi.GetFuturesContractsAsync), types).Signature);
            Assert.Equal(Before(nameof(IMarketDataApi.GetFuturesContractsAsync), types, args),
                MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetFuturesContractsAsync), types, args));
        })));
    }

    [Fact]
    public void Cancellation_and_serialization_failure_do_not_leak_rented_memory()
    {
        var pool = new TrackingPool();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetBackendStatusAsync),
            [typeof(CancellationToken)], [null], cancellation.Token, pool));
        Assert.Equal(0, pool.Outstanding);
        Assert.Throws<MessagePackSerializationException>(() => MarketDataApiCallCodec.Encode(nameof(IMarketDataApi.GetFuturesPriceAsync),
            [typeof(string)], [new Action(() => { })], pool: pool));
        Assert.Equal(0, pool.Outstanding);
        Assert.True(pool.AllReturnedBytesWereCleared);
    }

    static byte[] Before(string operation, Type[] types, object?[] arguments)
    {
        var method = typeof(IMarketDataApi).GetMethod(operation, types)!;
        var values = arguments.Select(value => value is null ? [] : MessagePackSerializer.Serialize(value.GetType(), value,
            MarketDataHostConnection.Serialization)).ToArray();
        return MarketDataHostConnection.Encode(new MarketDataApiCall(MarketDataApiClient.Signature(method), values));
    }

    sealed class TrackingPool : ArrayPool<byte>
    {
        readonly List<byte[]> returned = [];
        public int Outstanding { get; private set; }
        public bool AllReturnedBytesWereCleared { get; private set; } = true;
        public override byte[] Rent(int minimumLength)
        {
            Outstanding++;
            return new byte[Math.Max(256, minimumLength)];
        }
        public override void Return(byte[] array, bool clearArray = false)
        {
            Outstanding--;
            AllReturnedBytesWereCleared &= array.All(value => value == 0);
            returned.Add(array);
        }
        public void PoisonReturnedBuffers() { foreach (var array in returned) Array.Fill(array, (byte)0xA5); }
    }
}
