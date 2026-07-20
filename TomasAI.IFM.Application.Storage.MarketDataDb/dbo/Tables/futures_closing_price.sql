CREATE TABLE [dbo].[futures_closing_price] (
    [ContractId]   VARCHAR (64)  NOT NULL,
    [ValueDate]    DATE          NOT NULL,
    [ClosingPrice] REAL          NOT NULL,
    [CreatedOn]    DATETIME      NOT NULL,
    [CreatedBy]    VARCHAR (255) NULL
);

