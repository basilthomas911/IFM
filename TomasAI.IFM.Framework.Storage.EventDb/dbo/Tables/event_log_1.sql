CREATE TABLE [dbo].[event_log] (
    [EventId]            BIGINT       IDENTITY (1, 1) NOT NULL,
    [EventSourceId]      BIGINT       NOT NULL,
    [EventSourceVersion] BIGINT       NOT NULL,
    [EventTypeId]        BIGINT       NOT NULL,
    [EventData]          TEXT         NULL,
    [EventDate]          DATETIME     NOT NULL,
    [CommandId]          VARCHAR (64) NULL,
    CONSTRAINT [PK_event_log2] PRIMARY KEY CLUSTERED ([EventId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_event_source_id]
    ON [dbo].[event_log]([EventSourceId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_event_log2]
    ON [dbo].[event_log]([EventSourceId] ASC, [EventSourceVersion] ASC, [EventTypeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_event_source_type_id]
    ON [dbo].[event_log]([EventTypeId] ASC, [EventSourceId] ASC);

