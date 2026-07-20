CREATE TABLE [dbo].[event_entity_id] (
    [EntityId] BIGINT        IDENTITY (1, 1) NOT NULL,
    [Value]    VARCHAR (900) NOT NULL,
    CONSTRAINT [PK_event_entity_id] PRIMARY KEY CLUSTERED ([EntityId] ASC),
    CONSTRAINT [UK_value] UNIQUE NONCLUSTERED ([Value] ASC)
);

