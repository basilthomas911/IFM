using System.Data;

namespace TomasAI.IFM.Shared.Storage;

internal interface IObjectDataQueuedCommandMetadata
{
    string CommandName { get; }
    string CommandText { get; }
    string CommandLogText { get; }
    CommandType CommandType { get; }
    string? ProviderName { get; }
    object? ConnectionIdentity { get; }
}
