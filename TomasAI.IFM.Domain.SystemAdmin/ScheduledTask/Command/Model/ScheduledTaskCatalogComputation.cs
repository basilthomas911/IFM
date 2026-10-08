using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
/// <summary>Calculates deployment facts without modifying actor state.</summary>
public static class ScheduledTaskCatalogComputation
{
    /// <summary>Produces the next catalog revision for a validated deployment or host observation.</summary>
    public static ScheduledTaskCatalogChange Compute(ICommand<ScheduledTaskId> command, ScheduledTaskCatalog? catalog, DateTimeOffset now)
    {
        if (!command.EntityId.IsValid || command.CommandId == Guid.Empty) return Reject("IDENTITY.INVALID", "catalog and command identities are required");
        if (catalog is not null && catalog.Id != command.EntityId) return Reject("IDENTITY.MISMATCH", "catalog identity does not match the command");
        return command switch
        {
            RegisterScheduledTaskProjectCommand registration => Register(registration, catalog),
            RecordScheduledTaskHostCapabilityCommand observation => Observe(observation, catalog, now),
            _ => Reject("COMMAND.UNSUPPORTED", "unsupported catalog operation")
        };
    }
    /// <summary>Replaces one project by task key while retaining all other deployed projects.</summary>
    private static ScheduledTaskCatalogChange Register(RegisterScheduledTaskProjectCommand command, ScheduledTaskCatalog? catalog)
    {
        if (string.IsNullOrWhiteSpace(command.HostId) || string.IsNullOrWhiteSpace(command.Environment) || string.IsNullOrWhiteSpace(command.Project.TaskKey) || string.IsNullOrWhiteSpace(command.Project.ProjectName) || string.IsNullOrWhiteSpace(command.Project.ManifestVersion) || string.IsNullOrWhiteSpace(command.Project.ArtifactDigest) || command.Project.MaximumRuntimeSeconds is < 1 or > 86400) return Reject("PROJECT.INVALID", "deployment identity, artifact digest and bounded runtime are required");
        if (catalog is not null && (catalog.HostId != command.HostId || catalog.Environment != command.Environment)) return Reject("HOST.MISMATCH", "catalog cannot change its host/environment identity");
        var next = catalog ?? new() { Id = command.EntityId, HostId = command.HostId, Environment = command.Environment };
        return new(next with { Revision = next.Revision + 1, Projects = next.Projects.Where(p => p.TaskKey != command.Project.TaskKey).Append(command.Project).OrderBy(p => p.TaskKey, StringComparer.Ordinal).ToArray() });
    }
    /// <summary>Accepts only monotonic observations for the catalog's owning host.</summary>
    private static ScheduledTaskCatalogChange Observe(RecordScheduledTaskHostCapabilityCommand command, ScheduledTaskCatalog? catalog, DateTimeOffset now)
    {
        var capability = command.HostCapability;
        if (string.IsNullOrWhiteSpace(capability.HostId) || string.IsNullOrWhiteSpace(capability.Environment) || string.IsNullOrWhiteSpace(capability.Platform) || capability.Generation < 1 || capability.ObservedAtUtc > now || now - capability.ObservedAtUtc > TimeSpan.FromMinutes(2)) return Reject("CAPABILITY.INVALID", "host observation must be identified and current");
        if (catalog is not null && (catalog.HostId != capability.HostId || catalog.Environment != capability.Environment)) return Reject("HOST.MISMATCH", "observation belongs to another catalog host");
        if (catalog?.HostCapability is { } previous && (capability.Generation < previous.Generation || capability.Generation == previous.Generation && capability.ObservedAtUtc <= previous.ObservedAtUtc)) return Reject("CAPABILITY.STALE", "observation is older than the persisted host generation");
        var next = catalog ?? new() { Id = command.EntityId, HostId = capability.HostId, Environment = capability.Environment };
        return new(next with { Revision = next.Revision + 1, HostCapability = capability });
    }
    /// <summary>Creates a domain-specific catalog rejection.</summary>
    private static ScheduledTaskCatalogChange Reject(string code, string detail) => new(null, $"ScheduledTaskCatalog.{code};{detail}");
}
