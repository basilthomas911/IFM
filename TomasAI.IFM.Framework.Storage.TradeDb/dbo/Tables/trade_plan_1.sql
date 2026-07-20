CREATE TABLE [dbo].[trade_plan] (
    [Id]                 INT           IDENTITY (1, 1) NOT NULL,
    [TradePlanId]        VARCHAR (36)  NULL,
    [OrderId]            INT           NOT NULL,
    [TradeId]            INT           NULL,
    [TradeType]          VARCHAR (32)  NULL,
    [TradeDate]          DATE          NOT NULL,
    [ValueDate]          DATE          NOT NULL,
    [MaturityDate]       DATE          NOT NULL,
    [ActionDate]         DATETIME      NOT NULL,
    [ActionType]         VARCHAR (32)  NOT NULL,
    [ActionSubType]      VARCHAR (32)  NULL,
    [ActionState]        VARCHAR (32)  NOT NULL,
    [ActionReason]       VARCHAR (255) NULL,
    [TradePnl]           MONEY         NOT NULL,
    [ForwardLossRatio]   REAL          NOT NULL,
    [LossProbability]    REAL          NULL,
    [MScore]             REAL          NULL,
    [MaxProfit]          MONEY         NOT NULL,
    [MaxLoss]            MONEY         NOT NULL,
    [MinProfitTarget]    MONEY         NULL,
    [DailyProfitTarget]  MONEY         NOT NULL,
    [AssetPrice]         MONEY         NOT NULL,
    [AssetStdDev]        REAL          NOT NULL,
    [AssetMean]          REAL          NOT NULL,
    [AssetPriceChange]   REAL          NULL,
    [MarketTrend]        VARCHAR (32)  NOT NULL,
    [MarketVolatility]   VARCHAR (32)  NULL,
    [MarketDirection]    VARCHAR (32)  NULL,
    [VixVolatility]      VARCHAR (32)  NOT NULL,
    [TradeRisk]          VARCHAR (32)  NULL,
    [FiftyDayMA]         REAL          NOT NULL,
    [FiveDayXMA]    REAL          NOT NULL,
    [PutOTMProbability]  REAL          NULL,
    [CallOTMProbability] REAL          NULL,
    [ShortPutGamma]      REAL          NULL,
    [ShortCallGamma]     REAL          NULL,
    [GammaRisk]          VARCHAR (32)  NULL,
    [NetPrice]           MONEY         NULL,
    [ForwardPrice]       MONEY         NULL,
    [StopLossLimit]      REAL          NULL,
    [CreatedOn]          DATETIME      NULL,
    [CreatedBy]          VARCHAR (64)  NULL,
    CONSTRAINT [PK_trade_plan] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_trade_plan]
    ON [dbo].[trade_plan]([OrderId] ASC, [TradeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_trade_plan_id]
    ON [dbo].[trade_plan]([TradePlanId] ASC);

