using System.Data;

namespace TomasAI.IFM.Framework.Storage;

internal interface IObjectDataQueuedCommandMetadata
{
    string CommandName { get; }
    string CommandText { get; }
    string CommandLogText { get; }
    CommandType CommandType { get; }
    string? ProviderName { get; }
    object? ConnectionIdentity { get; }
}
