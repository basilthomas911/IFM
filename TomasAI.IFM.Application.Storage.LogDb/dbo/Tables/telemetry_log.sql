CREATE TABLE [dbo].[telemetry_log] (
    [SequenceId] BIGINT       IDENTITY (1, 1) NOT NULL,
    [Timestamp]  DATETIME     NOT NULL,
    [LogLevel]   VARCHAR (16) NOT NULL,
    [Message]    TEXT         NOT NULL,
    [ServiceId]  VARCHAR (64) NULL,
    CONSTRAINT [PK_telemetry_log_1] PRIMARY KEY CLUSTERED ([SequenceId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_telemetry_log]
    ON [dbo].[telemetry_log]([Timestamp] ASC);

