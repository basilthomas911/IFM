using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Ordered PostgreSQL bindings for scheduled-task queries.</summary>
internal readonly record struct ScheduledTaskDbParameters(object? [] Values) : IBindValue
{
    public object Bind() => Values.Select(value => value).ToArray();
}