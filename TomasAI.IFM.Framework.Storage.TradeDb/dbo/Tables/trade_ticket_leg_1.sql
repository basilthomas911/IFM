CREATE TABLE [dbo].[trade_ticket_leg] (
    [FundId]          INT          NOT NULL,
    [OrderId]         INT          NOT NULL,
    [TradeId]         INT          NOT NULL,
    [ContractId]      VARCHAR (64) NOT NULL,
    [Quantity]        INT          NOT NULL,
    [BidPrice]        MONEY        NOT NULL,
    [AskPrice]        MONEY        NOT NULL,
    [Commission]      MONEY        NULL,
    [OptionLegAction] VARCHAR (32) NOT NULL,
    [CreatedOn]       DATETIME     NULL,
    [CreatedBy]       VARCHAR (64) NULL,
    CONSTRAINT [PK_trade_ticket_detail_1] PRIMARY KEY CLUSTERED ([FundId] ASC, [OrderId] ASC, [TradeId] ASC, [ContractId] ASC)
);

