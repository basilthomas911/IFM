CREATE TABLE [dbo].[trade_live_feed] (
    [OrderId]  INT NOT NULL,
    [TradeId]  INT NOT NULL,
    [LiveFeed] BIT NULL,
    CONSTRAINT [PK_trade_live_feed] PRIMARY KEY CLUSTERED ([OrderId] ASC, [TradeId] ASC)
);

