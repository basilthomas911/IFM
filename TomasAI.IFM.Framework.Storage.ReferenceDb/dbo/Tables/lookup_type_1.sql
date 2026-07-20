CREATE TABLE [dbo].[lookup_type] (
    [LookupTypeName] VARCHAR (64)  NOT NULL,
    [ShortCode]      VARCHAR (32)  NOT NULL,
    [OrderId]        INT           NOT NULL,
    [Description]    VARCHAR (256) NOT NULL,
    [CreatedOn]      DATETIME      NULL,
    [CreatedBy]      VARCHAR (64)  NULL,
    CONSTRAINT [PK_lookup_type] PRIMARY KEY CLUSTERED ([LookupTypeName] ASC, [ShortCode] ASC)
);

