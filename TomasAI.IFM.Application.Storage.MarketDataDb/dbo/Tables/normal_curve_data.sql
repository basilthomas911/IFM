CREATE TABLE [dbo].[normal_curve_data] (
    [StdDevIndex]   FLOAT (53) NOT NULL,
    [StdDevPercent] FLOAT (53) NOT NULL,
    CONSTRAINT [PK_normal_curve_data] PRIMARY KEY CLUSTERED ([StdDevIndex] ASC)
);

