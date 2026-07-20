CREATE TABLE [dbo].[spread_distribution_job] (
    [JobId]                 INT            NOT NULL,
    [OrderId]               INT            NOT NULL,
    [TradeId]               INT            NOT NULL,
    [TradeType]             VARCHAR (32)   NOT NULL,
    [TradeStatus]           VARCHAR (32)   NOT NULL,
    [ValueDate]             DATE           NOT NULL,
    [DaysToExpiry]          INT            NOT NULL,
    [OptionStyle]           VARCHAR (32)   NOT NULL,
    [OptionType]            VARCHAR (32)   NOT NULL,
    [JobSubmitted]          DATETIME       NOT NULL,
    [JobStatus]             VARCHAR (4000) NULL,
    [JobCompleted]          DATETIME       NULL,
    [JobFailed]             DATETIME       NULL,
    [InProgress]            BIT            NOT NULL,
    [LossProbabilityFactor] REAL           NULL,
    CONSTRAINT [PK_spread_distribution_job] PRIMARY KEY CLUSTERED ([JobId] ASC)
);

