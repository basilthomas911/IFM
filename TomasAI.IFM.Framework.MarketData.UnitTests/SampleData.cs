using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

public static class SampleData
{
    public const int DefaultCapacity = 4;
    public const int SpinCountEnqueue = 2;
    public const int SpinCountDequeue = 2;

    public static readonly TestMessage Message1 = new(1, 100.25m);
    public static readonly TestMessage Message2 = new(2, 200.50m);
    public static readonly TestMessage Message3 = new(3, 300.75m);
    public static readonly TestMessage Message4 = new(4, 400.00m);
    public static readonly TestMessage Message5 = new(5, 500.25m);

    public static BlockingSpscRingBuffer<TestMessage> CreateBuffer(int capacity = DefaultCapacity)
    {
        var buffer = new BlockingSpscRingBuffer<TestMessage>(capacity, SpinCountEnqueue, SpinCountDequeue);
        buffer.Start();
        return buffer;
    }
}

public readonly record struct TestMessage(int Id, decimal Price);
