CREATE TABLE [dbo].[spread_distribution_path_value] (
    [Id]                   BIGINT IDENTITY (1, 1) NOT NULL,
    [SpreadDistributionId] BIGINT NOT NULL,
    [DaysToMaturity]       INT    NOT NULL,
    [SpreadValue]          REAL   NOT NULL,
    CONSTRAINT [PK_spread_path_value] PRIMARY KEY CLUSTERED ([Id] ASC)
);

