using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

static class IdempotentCommandId
{
    public static Guid Create<T>(Guid idempotencyKey, T message)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        var key = idempotencyKey.ToByteArray();
        var body = MessagePackSerializer.Serialize(message);
        var input = new byte[key.Length + body.Length];
        key.CopyTo(input, 0);
        body.CopyTo(input, key.Length);
        var hash = SHA256.HashData(input);
        return new Guid(hash.AsSpan(0, 16));
    }
}
