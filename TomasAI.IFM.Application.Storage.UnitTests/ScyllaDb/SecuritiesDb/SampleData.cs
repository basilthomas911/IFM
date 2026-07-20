using System;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.SecuritiesDb
{
    public static class SampleData
    {
        public static FuturesContractV2ReadModel FuturesContract
            => new (
                contractId: "ES20251010",
                description: "Test Description",
                symbol: "TEST",
                localSymbol: "TEST_LOCAL",
                securityType: "FUT",
                currency: "USD",
                exchange: "NYSE",
                multiplier: "100",
                lastTradeDate: DateOnly.FromDateTime(DateTime.UtcNow),
                currentlyTraded: true);

        public static FuturesOptionContractReadModel FuturesOptionContract
            => new (
                contractId: "ES20251010C2525",
                description: "Test Option Description",
                symbol: "TEST_OPT",
                localSymbol: "TEST_LOCAL_OPT",
                securityType: "FOP",
                currency: "USD",
                exchange: "NYSE",
                multiplier: "50",
                contractMonth: DateOnly.FromDateTime(DateTime.UtcNow),
                strikePrice: 1000,
                optionType: "Call");
    }


}
