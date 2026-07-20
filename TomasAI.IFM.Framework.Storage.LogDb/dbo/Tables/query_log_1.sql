CREATE TABLE [dbo].[query_log] (
    [QueryLogId]   BIGINT         IDENTITY (1, 1) NOT NULL,
    [CommandId]    VARCHAR (64)   NOT NULL,
    [QueryDate]    DATETIME       NOT NULL,
    [QueryName]    VARCHAR (4000) NOT NULL,
    [QueryData]    TEXT           NULL,
    [UserName]     VARCHAR (255)  NOT NULL,
    [ErrorMessage] VARCHAR (255)  NOT NULL,
    [ErrorCode]    INT            NOT NULL,
    [ErrorType]    VARCHAR (64)   NOT NULL,
    [ErrorData]    TEXT           NULL,
    CONSTRAINT [PK_query_log] PRIMARY KEY CLUSTERED ([QueryLogId] ASC)
);

