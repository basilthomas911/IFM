using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

/// <summary>Represents one bounded, low-cardinality CloudWatch metric observation.</summary>
public sealed record AwsCloudWatchMetricSample(
    string Name,
    double Value,
    StandardUnit Unit,
    DateTime TimestampUtc,
    IReadOnlyDictionary<string, string> Dimensions);

/// <summary>Buffers runtime metric observations without allowing an AWS outage to consume unbounded memory.</summary>
public sealed class AwsCloudWatchMetricBuffer
{
    readonly object _sync = new();
    readonly Queue<AwsCloudWatchMetricSample> _samples = new();
    readonly int _capacity;

    /// <summary>Initializes a bounded metric buffer.</summary>
    public AwsCloudWatchMetricBuffer(int capacity)
    {
        if (capacity is < 1000 or > 10000) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    /// <summary>Gets the number of metric observations discarded after the buffer reached capacity.</summary>
    public long DroppedCount { get; private set; }

    /// <summary>Adds one observation, or safely discards it when the fixed capacity is exhausted.</summary>
    public void Record(AwsCloudWatchMetricSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        lock (_sync)
        {
            if (_samples.Count >= _capacity)
            {
                DroppedCount++;
                return;
            }
            _samples.Enqueue(sample);
        }
    }

    /// <summary>Removes up to the requested number of observations for one export attempt.</summary>
    public IReadOnlyList<AwsCloudWatchMetricSample> Take(int maximum)
    {
        if (maximum is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maximum));
        lock (_sync)
        {
            var result = new List<AwsCloudWatchMetricSample>(Math.Min(maximum, _samples.Count));
            while (result.Count < maximum && _samples.TryDequeue(out var sample)) result.Add(sample);
            return result;
        }
    }

    /// <summary>Returns an unsuccessful export batch to the front of the bounded buffer.</summary>
    public void Return(IReadOnlyList<AwsCloudWatchMetricSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) return;
        lock (_sync)
        {
            var retained = samples.Concat(_samples).Take(_capacity).ToArray();
            DroppedCount += samples.Count + _samples.Count - retained.Length;
            _samples.Clear();
            foreach (var sample in retained) _samples.Enqueue(sample);
        }
    }
}

/// <summary>Exports buffered IFM database-backup observations to the approved CloudWatch namespace.</summary>
public sealed class AwsCloudWatchMetricExporter(
    IAmazonCloudWatch cloudWatch,
    AwsCloudDatabaseBackupOptions options,
    AwsCloudWatchMetricBuffer buffer)
{
    /// <summary>Exports all currently buffered observations in CloudWatch-sized batches.</summary>
    public async Task<int> ExportPendingAsync(CancellationToken cancellationToken)
    {
        var exported = 0;
        while (true)
        {
            // Tagged observations also emit an environment-only rollup, so 500 input
            // samples always fit within CloudWatch's 1,000-datum request limit.
            var samples = buffer.Take(500);
            if (samples.Count == 0) return exported;
            try
            {
                var request = CreateRequest(samples);
                await cloudWatch.PutMetricDataAsync(request, cancellationToken).ConfigureAwait(false);
                exported += samples.Count;
            }
            catch
            {
                buffer.Return(samples);
                throw;
            }
        }
    }

    /// <summary>Creates a CloudWatch request using only the fixed namespace and bounded metric dimensions.</summary>
    public PutMetricDataRequest CreateRequest(IReadOnlyList<AwsCloudWatchMetricSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(samples));
        var environment = new Dimension
        {
            Name = "environment",
            Value = options.Environment.ToString().ToLowerInvariant()
        };
        return new PutMetricDataRequest
        {
            Namespace = options.CloudWatchMetricNamespace,
            MetricData = samples.SelectMany(sample => CreateData(sample, environment)).ToList()
        };
    }

    static IEnumerable<MetricDatum> CreateData(AwsCloudWatchMetricSample sample, Dimension environment)
    {
        yield return CreateDatum(sample, sample.Dimensions
            .Select(static pair => new Dimension { Name = pair.Key, Value = pair.Value })
            .Append(environment)
            .OrderBy(static dimension => dimension.Name, StringComparer.Ordinal)
            .ToList());
        if (sample.Dimensions.Count > 0)
            yield return CreateDatum(sample, [environment]);
    }

    static MetricDatum CreateDatum(AwsCloudWatchMetricSample sample, List<Dimension> dimensions) => new()
    {
        MetricName = sample.Name,
        Value = sample.Value,
        Unit = sample.Unit,
        Timestamp = sample.TimestampUtc,
        Dimensions = dimensions
    };
}
