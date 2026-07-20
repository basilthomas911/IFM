CREATE TABLE [dbo].[event_service_log] (
    [EventServiceLogId] BIGINT         IDENTITY (1, 1) NOT NULL,
    [CommandId]         VARCHAR (64)   NOT NULL,
    [EventDate]         DATETIME       NOT NULL,
    [EventName]         VARCHAR (4000) NOT NULL,
    [EventData]         TEXT           NULL,
    [UserName]          VARCHAR (255)  NOT NULL,
    [ErrorMessage]      VARCHAR (255)  NOT NULL,
    [ErrorCode]         INT            NOT NULL,
    [ErrorType]         VARCHAR (64)   NOT NULL,
    [ErrorData]         TEXT           NULL,
    CONSTRAINT [PK_event_service_log] PRIMARY KEY CLUSTERED ([EventServiceLogId] ASC)
);

