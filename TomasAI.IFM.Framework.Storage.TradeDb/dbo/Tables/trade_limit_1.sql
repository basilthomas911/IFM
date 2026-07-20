CREATE TABLE [dbo].[trade_limit] (
    [TradeId]           INT          NOT NULL,
    [TradeType]         VARCHAR (32) NOT NULL,
    [RiskMargin]        MONEY        NOT NULL,
    [MaxProfit]         MONEY        NOT NULL,
    [MaxLoss]           MONEY        NULL,
    [MaxReturn]         REAL         NOT NULL,
    [MaxLossLimit]      REAL         NOT NULL,
    [MinProfitLimit]    REAL         NOT NULL,
    [MinProfitTarget]   MONEY        NOT NULL,
    [DailyProfitTarget] MONEY        NULL,
    [CreatedOn]         DATETIME     NULL,
    [CreatedBy]         VARCHAR (64) NULL,
    [UpdatedOn]         DATETIME     NULL,
    [UpdatedBy]         VARCHAR (64) NULL,
    CONSTRAINT [PK_trade_limit] PRIMARY KEY CLUSTERED ([TradeId] ASC)
);

