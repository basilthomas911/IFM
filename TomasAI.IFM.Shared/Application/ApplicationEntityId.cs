using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Application;

[MessagePackObject(AllowPrivate = true)]
public record ApplicationEntityId(
    [property: Key(0)] DateOnly ValueDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; defaults to current UTC year.
    /// </summary>
    public ApplicationEntityId() : this(DateOnly.FromDateTime(DateTime.UtcNow)) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    public string Format() 
        => ValueDate.ToString("yyyy-MM-dd");
}
