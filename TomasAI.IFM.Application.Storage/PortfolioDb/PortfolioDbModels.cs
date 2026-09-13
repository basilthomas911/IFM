namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public sealed record PortfolioProjection<T>(T Value,int SchemaVersion,long AggregateVersion,long SourceEventId,DateTime UpdatedOnUtc,string PayloadHash)
{
    public static PortfolioProjection<T> Create(T value,long aggregateVersion,long sourceEventId,DateTime updatedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        if(aggregateVersion<=0 || sourceEventId<=0) throw new ArgumentOutOfRangeException(nameof(aggregateVersion));
        if(updatedOnUtc.Kind!=DateTimeKind.Utc) throw new ArgumentException("Projection timestamp must be UTC.",nameof(updatedOnUtc));
        var payload=System.Text.Json.JsonSerializer.Serialize(value);
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return new(value,1,aggregateVersion,sourceEventId,updatedOnUtc,hash);
    }
}

public sealed record PortfolioProjectionRevision(int PortfolioId,int? FundId,long AggregateRevision,long SourceEventId);
public sealed record DraftFundProjectionDeletion(int FundId,long[] MandateVersions);
public sealed record DraftPortfolioProjectionDeletion(int PortfolioId,int StateBucket,DraftFundProjectionDeletion[] Funds,long SourceEventId);
public sealed record DraftPolicyProjectionDeletion(int PortfolioId,int PolicyId,long SourceEventId);
