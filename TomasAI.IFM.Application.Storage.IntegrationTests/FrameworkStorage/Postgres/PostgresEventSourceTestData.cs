using System;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.Postgres;

internal static class PostgresEventSourceTestData
{
    internal const int SlotCount = 16;
    internal const string Timestamp = "2026-01-15T13:30:00.0000000Z";
    internal const string UpdatedTimestamp = "2026-01-16T14:45:00.0000000Z";

    internal static PostgresEventSourceTestScope Scope(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slot, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slot, SlotCount);

        return new PostgresEventSourceTestScope(
            EventStreamId: -2_000_000_000 + slot,
            SecondEventStreamId: -1_900_000_000 + slot,
            EventNameId: -2_000_000_000 + slot,
            EventVersion: -2_000_000_000L + slot,
            SecondEventVersion: -1_900_000_000L + slot,
            CommandId: Guid.Parse($"00000000-0000-0000-0000-{slot:D12}"),
            Slot: slot);
    }
}
internal readonly record struct PostgresEventSourceTestScope(
    int EventStreamId,
    int SecondEventStreamId,
    int EventNameId,
    long EventVersion,
    long SecondEventVersion,
    Guid CommandId,
    int Slot)
{
    internal string EventStream => $"__framework_storage_postgres_it__:{Slot}:stream";
    internal string SecondEventStream => $"__framework_storage_postgres_it__:{Slot}:stream-2";
    internal string EventName => $"FrameworkStorageEvent{Slot}";
    internal string EventTypeName => $"Framework.Storage.Integration.Event{Slot}";
    internal string ProjectorName => $"framework-storage-projector-{Slot}";
}
