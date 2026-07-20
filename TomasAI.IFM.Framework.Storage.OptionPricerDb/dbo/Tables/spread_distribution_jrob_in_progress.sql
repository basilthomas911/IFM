CREATE TABLE [dbo].[spread_distribution_jrob_in_progress] (
    [JobId]   INT NOT NULL,
    [OrderId] INT NOT NULL,
    [TradeId] INT NOT NULL,
    CONSTRAINT [PK_spread_distribution_jrob_in_progress] PRIMARY KEY CLUSTERED ([JobId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_spread_distribution_jrob_in_progress]
    ON [dbo].[spread_distribution_jrob_in_progress]([OrderId] ASC, [TradeId] ASC);

