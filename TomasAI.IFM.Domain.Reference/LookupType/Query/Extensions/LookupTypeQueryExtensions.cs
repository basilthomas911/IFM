using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query.Extensions;
/// <summary>Provides readonly lookup-type services on the typed query context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class LookupTypeQueryExtensions
{
#pragma warning disable S3398 // C# 14 extension lowering diagnostic; this source has no redundant cast or local function to move.
    static ILookupTypeQueryContext Typed(IQueryActorContext<LookupTypeQueryActor> context) => IsArgumentNull.Named(context as ILookupTypeQueryContext, nameof(context))!;
#pragma warning restore S3398
    extension(IQueryActorContext<LookupTypeQueryActor> context)
    {
        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => Typed(context).DbFactory;
        /// <summary>Gets the query actor logger.</summary>
        public ILogger<LookupTypeQueryActor> Logger => Typed(context).Logger;
    }
}
