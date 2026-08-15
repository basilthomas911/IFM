using System.Runtime.InteropServices;
using System.Text;
using DatabentoFeed.Native.Interop;

namespace DatabentoFeed.Native.ComparisonTests;

public sealed unsafe class NativeParityTests
{
    private static string CppPath => RequiredPath("DBF_CPP_DLL");
    private static string RustPath => RequiredPath("DBF_RUST_DLL");

    [Fact]
    public void Both_libraries_expose_the_complete_frozen_abi()
    {
        using var cpp = new NativeApi(CppPath);
        using var rust = new NativeApi(RustPath);
        Assert.Equal(Dbf.AbiVersion, cpp.GetAbiVersion());
        Assert.Equal(cpp.GetAbiVersion(), rust.GetAbiVersion());
        Assert.All(NativeApi.CanonicalExports, name =>
        {
            Assert.True(cpp.HasExport(name), $"C++ is missing {name}");
            Assert.True(rust.HasExport(name), $"Rust is missing {name}");
        });
        Assert.Equal(128, sizeof(FeedConfigV1));
        Assert.Equal(32, sizeof(TickerSubscriptionV1));
        Assert.Equal(32, sizeof(TickerInstrumentMappingV1));
        Assert.Equal(32, sizeof(OptionChainSubscriptionV1));
        Assert.Equal(32, sizeof(OptionContractSelectionV1));
        Assert.Equal(32, sizeof(WaitResultV1));
        Assert.Equal(32, sizeof(BatchResultV1));
        Assert.Equal(128, sizeof(StatsV1));
        Assert.Equal(64, sizeof(ContractQueryV1));
        Assert.Equal(192, sizeof(ContractDetailV1));
        Assert.Equal(88, sizeof(LatestPriceRequestV1));
        Assert.Equal(64, sizeof(LatestPriceResult64));
        Assert.Equal(64, sizeof(MarketRecord64));
    }

    [Fact]
    public void Every_export_has_matching_validation_and_non_live_status_semantics()
    {
        using var cpp = new NativeApi(CppPath);
        using var rust = new NativeApi(RustPath);
        Assert.Equal(InvokeValidationMatrix(cpp), InvokeValidationMatrix(rust));
    }

    [Fact]
    public void Synthetic_lifecycle_mappings_records_and_stats_match_cpp()
    {
        using var cpp = new NativeApi(CppPath);
        using var rust = new NativeApi(RustPath);
        SyntheticRun expected = SyntheticFeedRunner.Run(cpp, 20_000, 512);
        SyntheticRun actual = SyntheticFeedRunner.Run(rust, 20_000, 512);
        Assert.Equal(expected.MappingCount, actual.MappingCount);
        Assert.Equal(expected.MappingBlob, actual.MappingBlob);
        Assert.Equal(expected.Mappings.Select(MappingIdentity), actual.Mappings.Select(MappingIdentity));
        Assert.Equal(expected.Records.Length, actual.Records.Length);
        for (int i = 0; i < expected.Records.Length; i++)
            Assert.Equal(RecordIdentity(expected.Records[i]), RecordIdentity(actual.Records[i]));
        Assert.Equal(expected.Stats.RingCapacityRecords, actual.Stats.RingCapacityRecords);
        Assert.Equal(expected.Stats.RecordsProduced, actual.Stats.RecordsProduced);
        Assert.Equal(expected.Stats.RecordsConsumed, actual.Stats.RecordsConsumed);
        Assert.Equal(expected.Stats.RingOverruns, actual.Stats.RingOverruns);
    }

    private static string MappingIdentity(TickerInstrumentMappingV1 value) =>
        $"{value.StructSize}:{value.AbiVersion}:{value.SubscriptionIndex}:{value.InstrumentId}:" +
        $"{value.PublisherId}:{value.RequestedSymbolOffset}:{value.RequestedSymbolLength}:" +
        $"{value.RawSymbolOffset}:{value.RawSymbolLength}";

    // Synthetic timestamps are deliberately generated from each library's monotonic clock.
    private static string RecordIdentity(MarketRecord64 value) =>
        $"{value.InstrumentId}:{value.PublisherId}:{value.RecordKind}:{value.Flags}:" +
        $"{value.Sequence}:{value.SourceSchema}:{value.Value0}:{value.Value1}:{value.Value2}:" +
        $"{(value.RecordKind == 2 ? 0 : value.Value3)}";

    private static int[] InvokeValidationMatrix(NativeApi api)
    {
        var statuses = new List<int>();
        nint handle = 0;
        uint count = 0, bytes = 0;
        MarketRecord64* records = null;
        FeedConfigV1 config = default;
        TickerSubscriptionV1 subscription = default;
        OptionChainSubscriptionV1 chain = default;
        OptionContractSelectionV1 selection = default;
        WaitResultV1 wait = default;
        BatchResultV1 batch = default;
        StatsV1 stats = default;
        ContractQueryV1 query = default;
        Utf8SliceV1 symbol = default;
        ContractDetailV1 detail = default;
        LatestPriceRequestV1 latest = default;
        LatestPriceResult64 latestResult = default;
        byte value = 0;
        statuses.Add((int)api.GetAbiVersion());
        statuses.Add(api.FeedCreate(&config, &value, 0, &handle));
        statuses.Add(api.SubscribeTickers(0, &subscription, 1, &value, 0, 1));
        statuses.Add(api.SubscribeOptionChain(0, &chain, &selection, 1, &value, 0, 1));
        statuses.Add(api.AllocateReadBuffer(0, 1, &records));
        statuses.Add(api.Start(0, 1));
        statuses.Add(api.GetMappingCounts(0, &count, &bytes));
        statuses.Add(api.CopyMappings(0, null, 0, null, 0));
        statuses.Add(api.SetConsumerReady(0, 1));
        statuses.Add(api.Wait(0, 1, &wait));
        statuses.Add(api.ReadBatch(0, records, 1, &batch));
        statuses.Add(api.Stop(0, 1));
        statuses.Add(api.FreeReadBuffer(0, records));
        statuses.Add(api.GetStats(0, &stats));
        statuses.Add(api.GetLastError(0, &value, 1, &bytes));
        statuses.Add(api.Destroy(0));
        statuses.Add(api.ContractQuery(&query, &symbol, &value, 1, &handle));
        statuses.Add(api.ContractGetCounts(0, &count, &bytes));
        statuses.Add(api.ContractCopy(0, &detail, 1, &value, 1));
        statuses.Add(api.ContractGetError(0, &value, 1, &bytes));
        statuses.Add(api.ContractDestroy(0));
        statuses.Add(api.GetLatestPrice(&latest, 1, &latestResult));
        return [.. statuses];
    }

    private static string RequiredPath(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Set {variable}.");
        return value;
    }
}
