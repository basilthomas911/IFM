CREATE TABLE [dbo].[montecarlo_batch_config] (
    [TradeId]    INT      NOT NULL,
    [TradeDate]  DATETIME NOT NULL,
    [MemorySize] BIGINT   NOT NULL,
    [BatchSize]  INT      NOT NULL,
    CONSTRAINT [PK_montecarlo_batch_config] PRIMARY KEY CLUSTERED ([TradeId] ASC, [TradeDate] ASC, [MemorySize] ASC)
);

