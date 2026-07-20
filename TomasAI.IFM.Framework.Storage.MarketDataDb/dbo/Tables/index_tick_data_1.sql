CREATE TABLE [dbo].[index_tick_data] (
    [ContractId] VARCHAR (64) NOT NULL,
    [TickDate]   DATETIME     NOT NULL,
    [TickTime]   BIGINT       NOT NULL,
    [Value]      REAL         NOT NULL,
    [ValueDate]  DATE         NULL,
    CONSTRAINT [PK_index_tick_data] PRIMARY KEY CLUSTERED ([ContractId] ASC, [TickTime] ASC)
);

