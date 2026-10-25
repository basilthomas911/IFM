using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public class TradeFixture : IDisposable
{
    public TradeFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        global::TomasAI.IFM.Framework.Messaging.Nats.NatsActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public IDataSerializer DataSerializer => ActorExtensions.DataSerializer!;
    public INatsSerializer<byte[]> MsgSerializer => global::TomasAI.IFM.Framework.Messaging.Nats.NatsActorExtensions.MsgSerializer!;

    public void Dispose() { }
}
