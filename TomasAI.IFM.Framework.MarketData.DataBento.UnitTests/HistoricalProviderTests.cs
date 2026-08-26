using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Framework.MarketData.DataBento.Historical;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class HistoricalProviderTests
{
    [Fact]
    public async Task SyntheticProviderExercisesEstimateBatchDownloadAndRangeWithoutLeakingHandles()
    {
        var baseline = SafeHistoricalResultHandle.ActiveHandleCount;
        var provider = new DatabentoHistoricalProvider(
            new DatabentoHistoricalProviderOptions
            {
                UseSyntheticProvider = true,
                MaximumBatchRecords = 16
            },
            TimeProvider.System);
        var request = new HistoricalProviderRequest
        {
            Dataset = "GLBX.MDP3",
            Symbols = ["ES.c.0"],
            Schema = HistoricalDataSchema.OhlcvOneMinute,
            Symbology = HistoricalSymbology.Continuous,
            StartUtc = DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            EndUtc = DateTimeOffset.Parse("2026-01-02T00:02:00Z"),
            RecordLimit = 2,
            RequestHash = "fixture"
        };

        var estimate = await provider.EstimateAsync(request, CancellationToken.None);
        Assert.Equal(0m, estimate.EstimatedCostUsd);
        Assert.Equal(2, estimate.EstimatedRecords);

        var job = await provider.SubmitBatchAsync(request, CancellationToken.None);
        Assert.Equal(HistoricalProviderJobState.Completed, job.State);
        Assert.Equal(job, await provider.GetBatchJobAsync(job.ProviderJobId, CancellationToken.None));
        var files = await provider.ListBatchFilesAsync(job.ProviderJobId, CancellationToken.None);
        var file = Assert.Single(files);

        var directory = Directory.CreateTempSubdirectory("ifm-history-");
        try
        {
            var path = Path.Combine(directory.FullName, file.FileName);
            await provider.DownloadBatchFileAsync(job.ProviderJobId, file, path, CancellationToken.None);
            Assert.Equal(file.SizeBytes, new FileInfo(path).Length);

            await using var fileReader = await provider.OpenFileAsync(
                path, file.Schema, 1, CancellationToken.None);
            var firstFileBatch = await fileReader.ReadNextAsync(CancellationToken.None);
            Assert.NotNull(firstFileBatch);
            Assert.Equal("SYNTH", Assert.Single(firstFileBatch.Records).Symbol);

            await using var range = await provider.OpenRangeAsync(
                request, 1, CancellationToken.None);
            var first = await range.ReadNextAsync(CancellationToken.None);
            var second = await range.ReadNextAsync(CancellationToken.None);
            Assert.False(first!.IsFinal);
            Assert.True(second!.IsFinal);
            Assert.Equal(5.0005m, first.Records[0].CloseOrPrice);
        }
        finally
        {
            directory.Delete(true);
        }

        Assert.Equal(baseline, SafeHistoricalResultHandle.ActiveHandleCount);
    }
}
