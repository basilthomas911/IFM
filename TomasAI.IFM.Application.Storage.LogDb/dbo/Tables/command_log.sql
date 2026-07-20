CREATE TABLE [dbo].[command_log] (
    [CommandLogId] BIGINT         IDENTITY (1, 1) NOT NULL,
    [CommandId]    VARCHAR (64)   NOT NULL,
    [CommandDate]  DATETIME       NOT NULL,
    [CommandName]  VARCHAR (4000) NOT NULL,
    [AggregateId]  VARCHAR (4000) NOT NULL,
    [RouteTo]      VARCHAR (64)   NOT NULL,
    [CommandData]  TEXT           NULL,
    [UserName]     VARCHAR (255)  NOT NULL,
    [ErrorMessage] VARCHAR (255)  NOT NULL,
    [ErrorCode]    INT            NOT NULL,
    [ErrorType]    VARCHAR (64)   NOT NULL,
    [ErrorData]    TEXT           NULL,
    CONSTRAINT [PK_command_log] PRIMARY KEY CLUSTERED ([CommandLogId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_command_log]
    ON [dbo].[command_log]([CommandId] ASC);

