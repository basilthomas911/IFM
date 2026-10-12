using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>A coherent, time-stamped local input to one recovery episode.</summary>
public sealed record DatasetRecoveryManifestSnapshot
{
    public DateOnly ValueDate { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public IReadOnlyList<string> RequiredDatasets { get; }
    public IReadOnlyList<DatasetSubscriptionManifest> Manifests { get; }

    public DatasetRecoveryManifestSnapshot(DateOnly valueDate, DateTimeOffset capturedAtUtc,
        IReadOnlyList<DatasetSubscriptionManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        if (valueDate == default || capturedAtUtc == default
            || manifests.Count is < 1 or > DatasetDesiredSubscriptionRegistry.MaximumDatasets
            || manifests.Any(item => item is null || item.ValueDate != valueDate)
            || manifests.Select(item => item.Dataset).Distinct(StringComparer.Ordinal).Count() != manifests.Count)
            throw new ArgumentException("Recovery requires a coherent, bounded manifest snapshot.", nameof(manifests));
        ValueDate = valueDate;
        CapturedAtUtc = capturedAtUtc;
        var ordered = manifests.OrderBy(item => item.Dataset, StringComparer.Ordinal).ToArray();
        Manifests = Array.AsReadOnly(ordered);
        RequiredDatasets = Array.AsReadOnly(ordered.Select(item => item.Dataset).ToArray());
    }
}
