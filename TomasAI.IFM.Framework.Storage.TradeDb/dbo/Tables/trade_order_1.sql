CREATE TABLE [dbo].[trade_order] (
    [OrderId]     INT           NOT NULL,
    [FundId]      INT           NOT NULL,
    [OrderDate]   DATETIME      NOT NULL,
    [OrderStatus] VARCHAR (32)  NOT NULL,
    [Reference]   VARCHAR (256) NULL,
    [Deleted]     BIT           NULL,
    [CreatedOn]   DATETIME      NULL,
    [CreatedBy]   VARCHAR (64)  NULL,
    [UpdatedOn]   DATETIME      NULL,
    [UpdatedBy]   VARCHAR (64)  NULL,
    CONSTRAINT [PK_trade_order] PRIMARY KEY CLUSTERED ([OrderId] ASC)
);

