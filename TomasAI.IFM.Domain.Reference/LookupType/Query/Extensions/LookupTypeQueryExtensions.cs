using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query.Extensions;

/// <summary>Provides readonly lookup-type services on the typed query context.</summary>
public static class LookupTypeQueryExtensions
{
    extension(IQueryActorContext<LookupTypeQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => Typed(context).DbFactory;
        /// <summary>Gets the query actor logger.</summary>
        public ILogger<LookupTypeQueryActor> Logger => Typed(context).Logger;
    }
    static ILookupTypeQueryContext Typed(IQueryActorContext<LookupTypeQueryActor> context)
        => IsArgumentNull.Set(context as ILookupTypeQueryContext, nameof(context))!;
}
