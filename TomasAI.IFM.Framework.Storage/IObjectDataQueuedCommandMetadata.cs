using System.Data;

namespace TomasAI.IFM.Framework.Storage;

internal interface IObjectDataQueuedCommandMetadata
{
    CommandType CommandType { get; }
    string? ProviderName { get; }
    object? ConnectionIdentity { get; }
}
