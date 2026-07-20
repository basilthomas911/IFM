CREATE TABLE [dbo].[futures_trend_direction] (
    [ContractId]       VARCHAR (32) NOT NULL,
    [ValueDate]        DATE         NOT NULL,
    [Timestamp]        DATETIME     NOT NULL,
    [LookbackInterval] INT          NOT NULL,
    [UpTrendCount]     INT          NOT NULL,
    [DownTrendCount]   INT          NOT NULL,
    [TrendDirection]   VARCHAR (32) NOT NULL,
    CONSTRAINT [PK_futures_trend_strength] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC, [Timestamp] ASC)
);

