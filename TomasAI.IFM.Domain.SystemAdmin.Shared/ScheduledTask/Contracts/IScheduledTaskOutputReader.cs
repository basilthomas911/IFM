using MessagePack;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Reads only retained output under a configured host artifact root.</summary>
public interface IScheduledTaskOutputReader
{
    /// <summary>Reads bounded UTF-8 output using the persisted run's relative artifact identity.</summary>
    ValueTask<ScheduledTaskOutputPage> ReadAsync(string relativeDirectory, long offset, CancellationToken cancellationToken);
}
