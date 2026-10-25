using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Reports authority from the latest immutable metrics snapshot.</summary>
public sealed class SupervisorHealthManager(ISupervisorActorMetricsState state) : ISupervisorHealthManager
{
    public SupervisorAuthorityState AuthorityState =>
        state.Current.CriticalActors > 0 || state.Current.FailedActors > 0
            ? SupervisorAuthorityState.AvailableWithDegradedCapability
            : SupervisorAuthorityState.Available;
}
