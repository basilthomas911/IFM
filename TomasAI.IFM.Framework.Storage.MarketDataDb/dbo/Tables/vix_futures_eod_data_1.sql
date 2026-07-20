CREATE TABLE [dbo].[vix_futures_eod_data] (
    [ContractId] VARCHAR (64) NOT NULL,
    [ValueDate]  DATE         NOT NULL,
    [OpenPrice]  REAL         NULL,
    [HighPrice]  REAL         NULL,
    [LowPrice]   REAL         NULL,
    [ClosePrice] REAL         NULL,
    [Volume]     INT          NULL,
    CONSTRAINT [PK_vix_futures_eod_data] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC)
);

