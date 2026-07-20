-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spBackupDatabase]
	-- Add the parameters for the stored procedure here
	@backupName varchar(256),
	@backupType char(4)
AS
BEGIN
	if @backupType = 'full'
		BACKUP DATABASE [eventdb] TO  URL = @backupName WITH  FORMAT, INIT,  NAME = N'eventdb-Full Database Backup', SKIP, NOREWIND, NOUNLOAD,  STATS = 10
	else
		BACKUP DATABASE [eventdb] TO  URL = @backupName WITH  DIFFERENTIAL , FORMAT, INIT,  NAME = N'eventdb-Full Database Backup', SKIP, NOREWIND, NOUNLOAD,  STATS = 10
END