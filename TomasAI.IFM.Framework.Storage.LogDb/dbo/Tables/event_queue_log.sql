CREATE TABLE [dbo].[event_queue_log] (
    [EventQueueId]       INT            IDENTITY (1, 1) NOT NULL,
    [EventId]            BIGINT         NOT NULL,
    [EventTypeName]      VARCHAR (256)  NOT NULL,
    [EventQueueStatus]   VARCHAR (32)   NOT NULL,
    [EventQueueDate]     DATETIME       NOT NULL,
    [EventFailedMessage] VARCHAR (4000) NULL,
    CONSTRAINT [PK_event_queue_log] PRIMARY KEY CLUSTERED ([EventQueueId] ASC)
);

