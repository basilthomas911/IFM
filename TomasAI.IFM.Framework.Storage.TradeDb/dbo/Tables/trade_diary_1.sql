CREATE TABLE [dbo].[trade_diary] (
    [EntryId]        INT           IDENTITY (1, 1) NOT NULL,
    [EntryDate]      DATETIME      NOT NULL,
    [OrderId]        INT           NOT NULL,
    [TradeId]        INT           NOT NULL,
    [ValueDate]      DATE          NOT NULL,
    [TradeStatus]    VARCHAR (32)  NOT NULL,
    [ActionSource]   VARCHAR (32)  NOT NULL,
    [ActionType]     VARCHAR (32)  NOT NULL,
    [ActionSubType]  VARCHAR (32)  NOT NULL,
    [ActionState]    VARCHAR (32)  NOT NULL,
    [ActionReason]   VARCHAR (255) NOT NULL,
    [ActionDataType] VARCHAR (255) NULL,
    [ActionData]     NTEXT         NULL,
    CONSTRAINT [PK_trade_diary] PRIMARY KEY CLUSTERED ([EntryId] ASC)
);

