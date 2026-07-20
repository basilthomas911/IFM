CREATE TABLE [dbo].[scheduled_job] (
    [JobId]               INT           NOT NULL,
    [JobName]             VARCHAR (255) NOT NULL,
    [JobSchedule]         VARCHAR (64)  NOT NULL,
    [JobScheduleDate]     DATETIME      NOT NULL,
    [JobScheduleInterval] REAL          NOT NULL,
    [TaskName]            VARCHAR (64)  NOT NULL,
    [TaskEnabled]         BIT           NOT NULL,
    [CreatedOn]           DATETIME      NULL,
    [CreatedBy]           VARCHAR (64)  NULL,
    [UpdatedOn]           DATETIME      NULL,
    [UpdatedBy]           VARCHAR (64)  NULL,
    CONSTRAINT [PK_schedule_job] PRIMARY KEY CLUSTERED ([JobId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_job_name]
    ON [dbo].[scheduled_job]([JobName] ASC);

