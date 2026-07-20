CREATE TABLE [dbo].[trade_plan_action] (
    [Id]            INT            IDENTITY (1, 1) NOT NULL,
    [TradePlanId]   VARCHAR (36)   NULL,
    [OrderId]       INT            NOT NULL,
    [TradeId]       INT            NOT NULL,
    [ValueDate]     DATE           NOT NULL,
    [ActionType]    VARCHAR (32)   NOT NULL,
    [ActionSubType] VARCHAR (32)   NOT NULL,
    [ActionState]   VARCHAR (32)   NOT NULL,
    [ActionDate]    DATETIME       NOT NULL,
    [ActionReason]  VARCHAR (4000) NULL,
    [CreatedOn]     DATETIME       NOT NULL,
    [CreatedBy]     VARCHAR (64)   NULL,
    CONSTRAINT [PK_trade_plan_action] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_trade_plan_action]
    ON [dbo].[trade_plan_action]([OrderId] ASC, [TradeId] ASC, [ValueDate] ASC);

