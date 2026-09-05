using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;

public enum SubscriptionDatasetAvailability { Recovering, Open, Closed }
public sealed record DesiredSubscriptionRoute(SubscriptionTickerKey Ticker, int EffectiveOwners);

/// <summary>Immutable host intent only, not the Stage 3 wire manifest or price-qualified realized state.</summary>
public sealed class DesiredSubscriptionManifest
{
    public Guid HostEpochId { get; }
    public string Scope { get; }
    public string Dataset { get; }
    public DateOnly ValueDate { get; }
    public long Revision { get; }
    public IReadOnlyList<SubscriptionLeaseView> Leases { get; }
    public IReadOnlyList<DesiredSubscriptionRoute> Routes { get; }
    public string Digest { get; }

    internal DesiredSubscriptionManifest(Guid hostEpochId, string scope, string dataset, DateOnly valueDate,
        long revision, IEnumerable<SubscriptionLeaseView> leases)
    {
        HostEpochId = hostEpochId;
        Scope = scope;
        Dataset = dataset;
        ValueDate = valueDate;
        Revision = revision;
        Leases = Array.AsReadOnly(leases.OrderBy(item => item.Token.LeaseId).ToArray());
        // Expand each unique logical target once, not once per owner. Thousands of owners sharing
        // one 512-option chain must not materialize millions of repeated option references.
        Routes = Array.AsReadOnly(Leases.GroupBy(item => item.Target)
            .SelectMany(group => Targets(group.Key).Distinct().Select(key => new DesiredSubscriptionRoute(key, group.Count())))
            .GroupBy(route => route.Ticker).Select(group => new DesiredSubscriptionRoute(group.Key, group.Sum(route => route.EffectiveOwners)))
            .OrderBy(route => route.Ticker.ProviderScope, StringComparer.Ordinal)
            .ThenBy(route => route.Ticker.ContractId, StringComparer.Ordinal)
            .ThenBy(route => route.Ticker.Schema, StringComparer.Ordinal)
            .ThenBy(route => route.Ticker.AssetKind).ToArray());
        Digest = SubscriptionIdentity.Digest(writer =>
        {
            writer.Write(HostEpochId.ToByteArray());
            writer.Write(Scope);
            writer.Write(Dataset);
            writer.Write(ValueDate.DayNumber);
            writer.Write(Revision);
            writer.Write(Leases.Count);
            foreach (var lease in Leases)
            {
                writer.Write(lease.Token.LeaseId.ToByteArray());
                writer.Write(lease.Token.Version);
                writer.Write(lease.Owner.Scope);
                writer.Write(lease.Owner.Owner.WorkflowType);
                writer.Write(lease.Owner.Owner.WorkflowId);
                writer.Write(lease.Owner.Owner.LegId);
                writer.Write((int)lease.Purpose);
                writer.Write(lease.Target.Chain is not null);
                if (lease.Target.Chain is { } chain) writer.Write(chain.ContractSetDigest);
                else
                {
                    SubscriptionIdentity.Write(writer, lease.Target.Ticker!);
                    writer.Write(lease.Target.Underlying is not null);
                    if (lease.Target.Underlying is { } underlying) SubscriptionIdentity.Write(writer, underlying);
                }
            }
        });
    }

    internal static IEnumerable<SubscriptionTickerKey> Targets(SubscriptionTarget target)
    {
        if (target.Ticker is { } ticker)
        {
            yield return ticker;
            if (target.Underlying is { } dependency) yield return dependency;
        }
        else
        {
            yield return target.Chain!.Underlying;
            foreach (var option in target.Chain.Options) yield return option;
        }
    }
}
