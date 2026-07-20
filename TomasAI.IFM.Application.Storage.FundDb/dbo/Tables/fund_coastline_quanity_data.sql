CREATE TABLE [dbo].[fund_coastline_quanity_data] (
    [FundId]      INT          NOT NULL,
    [ValueDate]   DATE         NOT NULL,
    [Quantity]    INT          NOT NULL,
    [MinQuantity] INT          NOT NULL,
    [MaxQuantity] INT          NOT NULL,
    [Interval]    REAL         NOT NULL,
    [CreatedBy]   VARCHAR (64) NOT NULL,
    [CreatedOn]   DATETIME     NOT NULL,
    [UpdatedBy]   VARCHAR (64) NULL,
    [UpdatedOn]   DATETIME     NULL,
    CONSTRAINT [PK_fund_coastline_quanity_data] PRIMARY KEY CLUSTERED ([FundId] ASC, [ValueDate] ASC)
);

