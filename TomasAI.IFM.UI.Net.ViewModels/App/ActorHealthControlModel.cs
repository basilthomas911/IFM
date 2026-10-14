using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.UI.Net.ViewModels.App;
/// <summary>Shared eligibility and generation-bound Supervisor dispatch for both desktop renderers.</summary>
public sealed class ActorHealthControlModel(ISupervisorCommandApi commands, SupervisorControlOptions options)
{
    public static SupervisorActorOperationKind[] Operations { get; } = Enum.GetValues<SupervisorActorOperationKind>().Where(x=>x!=SupervisorActorOperationKind.Stop).ToArray();
    public bool AuthorityAvailable(ActorHealthSnapshot? snapshot) => options.Enabled && snapshot is { ManualMutationEnabled:true }
        && snapshot.SupervisorAuthorityState != nameof(SupervisorAuthorityState.HostRecoveryRequired);
    public bool CanExecute(ActorHealthSnapshot? snapshot, ActorHealthThreadId? target, string reason) => AuthorityAvailable(snapshot)
        && !string.IsNullOrWhiteSpace(options.Requester) && options.Timeout > TimeSpan.Zero && options.Timeout <= TimeSpan.FromMinutes(10)
        && target is not null && !string.IsNullOrWhiteSpace(reason) && reason.Length <= 1024;
    static bool Same(ActorHealthThreadId a,ActorHealthThreadId b) => a.ActorType==b.ActorType && a.Name==b.Name && a.EntityId==b.EntityId;
    public async Task<string> ExecuteAsync(ActorHealthSnapshot snapshot, ActorHealthThreadId target, long generation,
        SupervisorActorOperationKind operation, string reason, CancellationToken cancellationToken = default)
    {
        if (!CanExecute(snapshot,target,reason)) throw new InvalidOperationException("Supervisor operation is unavailable or requires an operational reason.");
        if (!Operations.Contains(operation)) throw new ArgumentException("Unsupported Supervisor operation.");
        var current = snapshot.Actors.SelectMany(x=>x.Mailboxes).Any(x=>Same(x.ThreadId,target) && x.Generation==generation)
            || snapshot.Incidents.Any(x=>Same(x.ThreadId,target) && x.Generation==generation);
        if (!current) throw new InvalidOperationException("Selected mailbox generation is no longer present; refresh health.");
        var result = await commands.ExecuteActorOperationAsync(new(target.ActorType,target.Name,target.EntityId),generation,operation,
            options.Requester,reason.Trim(),options.Timeout,cancellationToken);
        if (!result.Success) throw new InvalidOperationException(result.ErrorMessage ?? "Supervisor operation was rejected.");
        return $"Supervisor command {result.Value?.Guid:D} accepted; refresh health for the projected outcome.";
    }
}
