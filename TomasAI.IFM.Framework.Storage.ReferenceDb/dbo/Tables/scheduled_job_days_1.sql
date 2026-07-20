CREATE TABLE [dbo].[scheduled_job_days] (
    [JobId]     INT NOT NULL,
    [Monday]    BIT NOT NULL,
    [Tuesday]   BIT NOT NULL,
    [Wednesday] BIT NOT NULL,
    [Thursday]  BIT NOT NULL,
    [Friday]    BIT NOT NULL,
    [Saturday]  BIT NOT NULL,
    [Sunday]    BIT NOT NULL,
    CONSTRAINT [PK_scheduled_job_days] PRIMARY KEY CLUSTERED ([JobId] ASC)
);

