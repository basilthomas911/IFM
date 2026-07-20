CREATE TABLE [dbo].[status_console_log] (
    [StatusLogId] INT            IDENTITY (1, 1) NOT NULL,
    [StatusDate]  DATETIME       NOT NULL,
    [StatusCode]  INT            NOT NULL,
    [Source]      VARCHAR (32)   NOT NULL,
    [Message]     VARCHAR (256)  NOT NULL,
    [Data]        TEXT           NULL,
    [DataType]    VARCHAR (4000) NULL,
    CONSTRAINT [PK_status_log] PRIMARY KEY CLUSTERED ([StatusLogId] ASC)
);

