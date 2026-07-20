CREATE TABLE [dbo].[fund] (
    [FundId]       INT            NOT NULL,
    [Name]         VARCHAR (64)   NOT NULL,
    [Description]  VARCHAR (4000) NULL,
    [Balance]      MONEY          NULL,
    [IsProduction] BIT            NULL,
    [CreatedOn]    DATETIME       NOT NULL,
    [CreatedBy]    VARCHAR (256)  NULL,
    CONSTRAINT [PK_fund_1] PRIMARY KEY CLUSTERED ([FundId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [fund_index_name]
    ON [dbo].[fund]([Name] ASC);

