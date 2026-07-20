CREATE TABLE [dbo].[strike_price_volatility] (
    [Symbol]                VARCHAR (10) NOT NULL,
    [TradeType]             VARCHAR (32) NOT NULL,
    [MarketTrend]           VARCHAR (32) NOT NULL,
    [MarketVolatility]      VARCHAR (32) NOT NULL,
    [MarketVolatilityTrend] VARCHAR (32) NOT NULL,
    [Delta]                 INT          NOT NULL,
    [StrikePriceOffset]     INT          NOT NULL,
    CONSTRAINT [PK_strike_price_volatility_map] PRIMARY KEY CLUSTERED ([Symbol] ASC, [TradeType] ASC, [MarketTrend] ASC, [MarketVolatility] ASC, [MarketVolatilityTrend] ASC)
);

