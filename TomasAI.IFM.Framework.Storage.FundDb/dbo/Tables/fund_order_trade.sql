CREATE TABLE [dbo].[fund_order_trade] (
    [OrderId]            INT           NOT NULL,
    [TradeId]            INT           NOT NULL,
    [TradeType]          VARCHAR (32)  NOT NULL,
    [TradeDate]          DATE          NOT NULL,
    [MaturityDate]       DATE          NOT NULL,
    [TradeState]         VARCHAR (32)  NOT NULL,
    [TradeAction]        VARCHAR (32)  NOT NULL,
    [Reference]          VARCHAR (255) NULL,
    [PrimaryTrade]       BIT           NULL,
    [Sequence]           INT           IDENTITY (1, 1) NOT NULL,
    [BaseContractSymbol] CHAR (5)      NULL,
    [CreatedOn]          DATETIME      NULL,
    [CreatedBy]          VARCHAR (64)  NULL,
    CONSTRAINT [PK_trade_order_2] PRIMARY KEY CLUSTERED ([OrderId] ASC, [TradeId] ASC)
);

