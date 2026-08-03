using System;
using System.Collections.Generic;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

internal static class ScyllaFundTestData
{
    internal const int SlotCount = 16;

    internal static readonly DateTime CreatedOn = new(2026, 1, 15, 13, 30, 0, DateTimeKind.Utc);
    internal static readonly DateTime UpdatedOn = new(2026, 1, 16, 14, 45, 0, DateTimeKind.Utc);
    internal static readonly DateOnly TradeDate = new(2026, 1, 15);
    internal static readonly DateOnly MaturityDate = new(2026, 6, 19);
    internal static readonly DateOnly ValueDate = new(2026, 1, 16);

    internal static ScyllaFundTestScope Scope(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slot, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slot, SlotCount);

        var fundId = -2_000_000_000 + slot;
        return new ScyllaFundTestScope(
            fundId,
            OrderId: -(slot * 10 + 1),
            SecondOrderId: -(slot * 10 + 2),
            TradeId: -(slot * 100 + 1),
            TransactionId: -2_000_000_000L + slot);
    }
}

internal readonly record struct ScyllaFundTestScope(
    int FundId,
    int OrderId,
    int SecondOrderId,
    int TradeId,
    long TransactionId)
{
    internal IEnumerable<int> OrderIds
    {
        get
        {
            yield return OrderId;
            yield return SecondOrderId;
        }
    }
}
