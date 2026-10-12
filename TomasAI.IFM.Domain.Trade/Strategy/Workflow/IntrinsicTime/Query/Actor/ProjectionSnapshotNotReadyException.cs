using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
/// <summary>Signals that a projection exists behind the caller's required workflow revision.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This domain failure requires structured reason or stream/revision evidence; generic constructors would create failures without the identity required by its consumer.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The domain exception requires structured reason or stream/revision evidence; generic constructors would omit facts required by its consumer.")]
public sealed class ProjectionSnapshotNotReadyException : Exception
{
    /// <summary>Initializes a minimum-revision projection error.</summary>
    public ProjectionSnapshotNotReadyException(string identity, long minimumRevision, long? actualRevision) : base($"SnapshotNotReady: {identity} requires revision {minimumRevision}; current revision is {actualRevision?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "missing"}.")
    {
    }
}
