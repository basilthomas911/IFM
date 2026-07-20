CREATE TABLE [dbo].[trade_fill] (
    [TradeFillId] INT          IDENTITY (1, 1) NOT NULL,
    [TradeId]     INT          NOT NULL,
    [ContractId]  VARCHAR (32) NOT NULL,
    [FillDate]    DATETIME     NOT NULL,
    [Price]       MONEY        NOT NULL,
    [Quantity]    INT          NOT NULL,
    [Commission]  MONEY        NOT NULL,
    [CreatedOn]   DATETIME     NULL,
    [CreatedBy]   VARCHAR (64) NULL,
    CONSTRAINT [PK_trade_fill] PRIMARY KEY CLUSTERED ([TradeFillId] ASC)
);

