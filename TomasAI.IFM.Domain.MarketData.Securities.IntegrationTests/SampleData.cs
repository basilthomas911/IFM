using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.Securities.IntegrationTests;

/// <summary>
/// Provides sample data instances for use in securities integration tests.
/// </summary>
public static class SampleData
{
    static readonly DateOnly _lastTradeDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));

    public static FuturesContractV2ReadModel NewFuturesContract => new(
        contractId: "ES20251010",
        description: "Test Futures Contract",
        symbol: "ES",
        localSymbol: "ESH25",
        securityType: "FUT",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        lastTradeDate: _lastTradeDate,
        currentlyTraded: true
    );

    public static FuturesContractV2ReadModel ChangedFuturesContract => new(
        contractId: "ES20251010",
        description: "Changed Test Futures Contract",
        symbol: "ES",
        localSymbol: "ESH25-CHANGED",
        securityType: "FUT",
        currency: "USD",
        exchange: "GLOBEX",
        multiplier: "50",
        lastTradeDate: _lastTradeDate,
        currentlyTraded: false
    );

    public static FuturesOptionContractReadModel NewFuturesOptionContract => new(
        contractId: "ES20251010C5000",
        description: "Test Futures Option Contract",
        symbol: "ES",
        localSymbol: "EW1K6 C5000",
        securityType: "FOP",
        currency: "USD",
        exchange: "CME",
        multiplier: "50",
        contractMonth: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
        strikePrice: 5000.0,
        optionType: "Call"
    );

    public static FuturesOptionContractReadModel ChangedFuturesOptionContract => new(
        contractId: "ES20251010C5100",
        description: "Changed Test Futures Option Contract",
        symbol: "ES",
        localSymbol: "EW1K6 C5100",
        securityType: "FOP",
        currency: "USD",
        exchange: "GLOBEX",
        multiplier: "50",
        contractMonth: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
        strikePrice: 5100.0,
        optionType: "Call"
    );

    public static FuturesOptionContractReadModel[] NewFuturesOptionContracts =>
    [
        new(
            contractId: "ES20251010C5200",
            description: "Test Futures Option Contract Call 1",
            symbol: "ES",
            localSymbol: "EW1K6 C5200",
            securityType: "FOP",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            contractMonth: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            strikePrice: 5200.0,
            optionType: "Call"
        ),
        new(
            contractId: "ES20251010P4900",
            description: "Test Futures Option Contract Put 1",
            symbol: "ES",
            localSymbol: "EW1K6 P4900",
            securityType: "FOP",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            contractMonth: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            strikePrice: 4900.0,
            optionType: "Put"
        ),
        new(
            contractId: "ES20251010C1800",
            description: "Test Futures Option Contract Call 2",
            symbol: "ES",
            localSymbol: "EW1K6 C1800",
            securityType: "FOP",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            contractMonth: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            strikePrice: 1800.0,
            optionType: "Call"
        )
    ];

    public static YieldCurveRateReadModel NewYieldCurveRate => new(
        valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
        oneMonth: 4.25,
        twoMonth: 4.35,
        threeMonth: 4.45,
        sixMonth: 4.55,
        oneYear: 4.65,
        twoYear: 4.75,
        threeYear: 4.85,
        fiveYear: 4.95,
        sevenYear: 5.05,
        tenYear: 5.15,
        twentyYear: 5.25,
        thirtyYear: 5.35
    );

    public static YieldCurveRateReadModel ChangedYieldCurveRate => new(
        valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
        oneMonth: 4.30,
        twoMonth: 4.40,
        threeMonth: 4.50,
        sixMonth: 4.60,
        oneYear: 4.70,
        twoYear: 4.80,
        threeYear: 4.90,
        fiveYear: 5.00,
        sevenYear: 5.10,
        tenYear: 5.20,
        twentyYear: 5.30,
        thirtyYear: 5.40
    );

    public static YieldCurveRateReadModel[] ImportYieldCurveRates =>
    [
        new(
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            oneMonth: 4.10,
            twoMonth: 4.20,
            threeMonth: 4.30,
            sixMonth: 4.40,
            oneYear: 4.50,
            twoYear: 4.60,
            threeYear: 4.70,
            fiveYear: 4.80,
            sevenYear: 4.90,
            tenYear: 5.00,
            twentyYear: 5.10,
            thirtyYear: 5.20
        ),
        new(
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            oneMonth: 4.15,
            twoMonth: 4.25,
            threeMonth: 4.35,
            sixMonth: 4.45,
            oneYear: 4.55,
            twoYear: 4.65,
            threeYear: 4.75,
            fiveYear: 4.85,
            sevenYear: 4.95,
            tenYear: 5.05,
            twentyYear: 5.15,
            thirtyYear: 5.25
        ),
        new(
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            oneMonth: 4.20,
            twoMonth: 4.30,
            threeMonth: 4.40,
            sixMonth: 4.50,
            oneYear: 4.60,
            twoYear: 4.70,
            threeYear: 4.80,
            fiveYear: 4.90,
            sevenYear: 5.00,
            tenYear: 5.10,
            twentyYear: 5.20,
            thirtyYear: 5.30
        )
    ];
}
