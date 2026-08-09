# Messaging.Nats benchmarks

Run the performance suite from the repository root in Release mode:

```powershell
dotnet run -c Release --project TomasAI.IFM.Framework.Messaging.Nats.Benchmarks -- --filter '*'
```

For a quicker development check, append `--job short`. BenchmarkDotNet writes detailed reports under `BenchmarkDotNet.Artifacts`; those generated files are intentionally not source-controlled.

The suite measures the hot paths that do not require an external server: NATS payload serialization/copying, MessagePack envelopes, command/query ownership, shared event fan-out at 1/2/5/17 mailbox destinations, GC allocations, SPSC actor-message handoff, and Channel/MPSC/SPSC actor-mailbox throughput and retained allocation. Mailbox comparisons use isolated benchmark processes and persistent dedicated producer threads; the SPSC capacity comparison covers 8,192 and 65,536 slots. Network throughput and latency should be measured separately against the same NATS server and topology used in production so loopback transport does not hide backpressure or broker effects.

`SpscRing` is the recommended production mailbox implementation for the verified one-stripe-producer/one-scheduled-consumer topology. Checked-in production configuration remains `Channel` at capacity 8,192 until the recommendation is reviewed under paper-trading or initial-production traffic.
