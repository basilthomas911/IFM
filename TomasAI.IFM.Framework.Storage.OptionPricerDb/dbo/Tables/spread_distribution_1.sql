CREATE TABLE [dbo].[spread_distribution] (
    [Id]                 BIGINT       IDENTITY (1, 1) NOT NULL,
    [TradeId]            INT          NOT NULL,
    [TradeType]          VARCHAR (32) NULL,
    [TradeStatus]        VARCHAR (64) NOT NULL,
    [ValueDate]          DATE         NOT NULL,
    [DaysToExpiry]       INT          NOT NULL,
    [ShortVolatility]    REAL         NULL,
    [LongVolatility]     REAL         NULL,
    [ForwardPrice]       REAL         NULL,
    [LossProbability]    REAL         NULL,
    [LossThreshold]      MONEY        NULL,
    [LossThresholdCount] INT          NULL,
    [CreatedOn]          DATETIME     NULL,
    CONSTRAINT [PK_spread_distribution] PRIMARY KEY CLUSTERED ([Id] ASC)
);

