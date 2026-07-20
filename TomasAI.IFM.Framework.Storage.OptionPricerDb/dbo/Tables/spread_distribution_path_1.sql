CREATE TABLE [dbo].[spread_distribution_path] (
    [Id]                   BIGINT IDENTITY (1, 1) NOT NULL,
    [SpreadDistributionId] BIGINT NOT NULL,
    [DaysToMaturity]       INT    NOT NULL,
    [AveragePrice]         REAL   NOT NULL,
    CONSTRAINT [PK_spread_path_data] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_spread_path]
    ON [dbo].[spread_distribution_path]([SpreadDistributionId] ASC, [DaysToMaturity] ASC);

