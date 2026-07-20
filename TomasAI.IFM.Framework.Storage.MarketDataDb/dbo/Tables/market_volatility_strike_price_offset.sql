CREATE TABLE [dbo].[market_volatility_strike_price_offset] (
    [Symbol]            VARCHAR (10) NOT NULL,
    [MarketTrend]       VARCHAR (32) NOT NULL,
    [MarketVolatility]  VARCHAR (32) NOT NULL,
    [StrikePriceOffset] MONEY        NOT NULL,
    CONSTRAINT [PK_market_volatility_strike_price_offset] PRIMARY KEY CLUSTERED ([Symbol] ASC, [MarketTrend] ASC, [MarketVolatility] ASC)
);

