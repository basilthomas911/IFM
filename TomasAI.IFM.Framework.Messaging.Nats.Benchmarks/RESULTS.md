# Messaging.Nats performance results

Benchmark host: AMD Ryzen Threadripper 1950X, Windows 10 22H2, .NET 10.0.10, x64 RyuJIT, concurrent workstation GC. BenchmarkDotNet 0.15.8; 3 warmups and 8 measured iterations.

## Before optimization

Captured from commit `c799fa8` plus only the benchmark harness. Lower is better.

| Hot path | Payload | Mean | Allocated/op |
|---|---:|---:|---:|
| NATS `byte[]` deserialize, one segment | 256 B | 33.58 ns | 280 B |
| NATS `byte[]` deserialize, multiple segments | 256 B | 61.61 ns | 280 B |
| NATS `byte[]` serialize | 256 B | 12.00 ns | 0 B |
| MessagePack envelope serialize | 256 B | 712.04 ns | 400 B |
| MessagePack envelope deserialize | 256 B | 675.62 ns | 416 B |
| NATS `byte[]` deserialize, one segment | 4,096 B | 261.43 ns | 4,120 B |
| NATS `byte[]` deserialize, multiple segments | 4,096 B | 274.08 ns | 4,120 B |
| NATS `byte[]` serialize | 4,096 B | 80.56 ns | 0 B |
| MessagePack envelope serialize | 4,096 B | 2,639.74 ns | 4,256 B |
| MessagePack envelope deserialize | 4,096 B | 1,348.98 ns | 4,256 B |
| SPSC enqueue/dequeue pair | n/a | 224.4 ns | 0 B |

## After optimization

Captured on the same host, runtime, warmup count, and iteration count.

| Optimized hot path | Before | After | Change | Allocated before | Allocated after |
|---|---:|---:|---:|---:|---:|
| MessagePack + NATS output, 256 B payload | 724.04 ns | 661.01 ns | 8.7% faster | 400 B | 0 B |
| MessagePack + NATS output, 4,096 B payload | 2,720.30 ns | 2,003.67 ns | 26.3% faster | 4,256 B | 0 B |
| SPSC enqueue/dequeue pair | 224.4 ns | 12.36 ns | 94.5% faster | 0 B | 0 B |

The old outbound value combines the original `MessagePack envelope serialize` and `NATS byte[] serialize` stages because production previously executed both. The new typed serializer writes MessagePack directly to NATS's pooled `IBufferWriter<byte>`.

## Command inbound owned-memory stage

The command consumer now receives `NatsMemoryOwner<byte>` and transfers that owner through striped dispatch and the actor mailbox. The command actor deserializes directly from the owned pooled sequence and releases the payload immediately after parsing. These measurements isolate the inbound copy that this removes; allocations for the deserialized command object itself remain expected.

| Command inbound hot path | Payload | Mean | Allocated/op |
|---|---:|---:|---:|
| Legacy `byte[]` copy + MessagePack | 256 B | 757.65 ns | 816 B |
| Direct MessagePack from owned pooled sequence | 256 B | 677.75 ns | 416 B |
| Legacy `byte[]` copy + MessagePack | 4,096 B | 1,875.96 ns | 8,512 B |
| Direct MessagePack from owned pooled sequence | 4,096 B | 1,453.07 ns | 4,256 B |

This removes 400 B per 256-byte command (10.5% faster in this benchmark) and 4,256 B per 4 KB command (22.5% faster). Queries and events intentionally remain on `byte[]` until their separate ownership stages; events require a fan-out ownership design for routed mailboxes.
