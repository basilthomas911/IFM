CREATE TABLE [dbo].[option_trade] (
    [OrderId]              INT           NOT NULL,
    [TradeId]              INT           NOT NULL,
    [TradeStrategy]        VARCHAR (256) NULL,
    [TradeDate]            DATE          NOT NULL,
    [MaturityDate]         DATE          NOT NULL,
    [TradeType]            VARCHAR (64)  NOT NULL,
    [TradeState]           VARCHAR (32)  NOT NULL,
    [TradeAction]          VARCHAR (64)  NULL,
    [UnderlyingContractId] VARCHAR (64)  NOT NULL,
    [UnderlyingAssetType]  VARCHAR (32)  NULL,
    [IsPrimaryTrade]       BIT           CONSTRAINT [DF_spread_trade_IsPrimaryTrade] DEFAULT ((0)) NULL,
    [IsHedgeTrade]         BIT           CONSTRAINT [DF_spread_trade_IsHedgeTrade] DEFAULT ((0)) NULL,
    [CreatedOn]            DATETIME      NULL,
    [CreatedBy]            VARCHAR (64)  NULL,
    [UpdatedOn]            DATETIME      NULL,
    [UpdatedBy]            VARCHAR (64)  NULL,
    CONSTRAINT [PK_spread_trade] PRIMARY KEY CLUSTERED ([OrderId] ASC, [TradeId] ASC)
);

