CREATE TABLE [dbo].[market_exchanges] (
    [Symbol]      CHAR (5)     NOT NULL,
    [Exchange]    VARCHAR (10) NOT NULL,
    [DayOfWeek]   VARCHAR (10) NOT NULL,
    [MarketOpen]  CHAR (5)     NULL,
    [MarketClose] CHAR (5)     NULL,
    CONSTRAINT [PK_equity_market] PRIMARY KEY CLUSTERED ([Symbol] ASC, [Exchange] ASC, [DayOfWeek] ASC)
);

