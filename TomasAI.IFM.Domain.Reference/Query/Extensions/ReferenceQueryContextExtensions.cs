using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.Query.Extensions;

/// <summary>Provides readonly Reference services on the typed query context.</summary>
public static partial class ReferenceQueryExtensions
{
    extension(IQueryActorContext<ReferenceQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => Typed(context).DbFactory;
        /// <summary>Gets the query actor logger.</summary>
        public ILogger<ReferenceQueryActor> Logger => Typed(context).Logger;
    }
    static IReferenceQueryContext Typed(IQueryActorContext<ReferenceQueryActor> context)
        => IsArgumentNull.Set(context as IReferenceQueryContext, nameof(context))!;
}
