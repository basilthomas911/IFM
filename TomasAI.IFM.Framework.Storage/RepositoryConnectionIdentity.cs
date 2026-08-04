using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Supplies an opaque value identity for one provider/base-connection pair.
/// The connection string is represented by a SHA-256 digest so queued commands can
/// be validated without retaining credentials in a process-wide collection.
/// </summary>
internal static class RepositoryConnectionIdentity
{
    static readonly ConditionalWeakTable<IObjectRepository, Identity> Identities = new();

    public static object Get(IObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Identities.GetValue(repository, static key => Create(key));
    }

    static Identity Create(IObjectRepository repository)
    {
        var connectionBytes = Encoding.UTF8.GetBytes(repository.ConnectionString ?? string.Empty);
        try
        {
            var digest = Convert.ToHexString(SHA256.HashData(connectionBytes));
            return new Identity(repository.ProviderName ?? string.Empty, digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(connectionBytes);
        }
    }

    sealed class Identity : IEquatable<Identity>
    {
        readonly string _providerName;
        readonly string _connectionDigest;

        public Identity(string providerName, string connectionDigest)
        {
            _providerName = providerName;
            _connectionDigest = connectionDigest;
        }

        public bool Equals(Identity? other)
            => other is not null &&
               string.Equals(_providerName, other._providerName, StringComparison.Ordinal) &&
               string.Equals(_connectionDigest, other._connectionDigest, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as Identity);

        public override int GetHashCode()
            => HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(_providerName),
                StringComparer.Ordinal.GetHashCode(_connectionDigest));
    }
}
