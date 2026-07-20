CREATE TABLE [dbo].[trade_plan_summary] (
    [OrderId]          INT           NOT NULL,
    [TradeId]          INT           NOT NULL,
    [TradeType]        VARCHAR (32)  NOT NULL,
    [TradeDate]        DATE          NOT NULL,
    [ValueDate]        DATE          NOT NULL,
    [MaturityDate]     DATE          NOT NULL,
    [ActionType]       VARCHAR (32)  NOT NULL,
    [ActionSubType]    VARCHAR (32)  NOT NULL,
    [ActionState]      VARCHAR (32)  NOT NULL,
    [MarketTrend]      VARCHAR (32)  NOT NULL,
    [MarketVolatility] VARCHAR (32)  NOT NULL,
    [ActionDate]       DATETIME      NOT NULL,
    [ActionReason]     VARCHAR (255) NULL,
    [TradePnl]         MONEY         NOT NULL,
    [ForwardLossRatio] REAL          NOT NULL,
    [LossProbability]  REAL          NULL,
    [MScore]           REAL          NOT NULL,
    [AssetPrice]       MONEY         NOT NULL,
    [AssetStdDev]      REAL          NOT NULL,
    [AssetMean]        REAL          NOT NULL,
    [AssetPriceChange] REAL          NOT NULL,
    [NetPrice]         MONEY         NOT NULL,
    [ForwardPrice]     MONEY         NOT NULL,
    [StopLossLimit]    REAL          NOT NULL,
    [CreatedOn]        DATETIME      NOT NULL,
    [CreatedBy]        VARCHAR (64)  NULL
);


GO
CREATE NONCLUSTERED INDEX [IX_trade_plan_summary]
    ON [dbo].[trade_plan_summary]([OrderId] ASC, [TradeId] ASC, [ActionDate] DESC);

