CREATE TABLE [dbo].[futures_option_tick_data] (
    [ContractId]        VARCHAR (128) NOT NULL,
    [TickDate]          DATE          NOT NULL,
    [TickTime]          BIGINT        NOT NULL,
    [OptionPrice]       REAL          NOT NULL,
    [BidPrice]          REAL          NOT NULL,
    [AskPrice]          REAL          NOT NULL,
    [BidSize]           INT           NOT NULL,
    [AskSize]           INT           NOT NULL,
    [ImpliedVolatility] REAL          NOT NULL,
    [Delta]             REAL          NOT NULL,
    [Gamma]             REAL          NOT NULL,
    [Vega]              REAL          NOT NULL,
    [Theta]             REAL          CONSTRAINT [DF_futures_option_tick_data_Theta] DEFAULT ((0)) NOT NULL,
    [Rho]               REAL          NULL,
    [UnderlyingPrice]   REAL          NOT NULL
);


GO
CREATE NONCLUSTERED INDEX [IX_futures_option_tick_data]
    ON [dbo].[futures_option_tick_data]([ContractId] ASC, [TickDate] ASC);

