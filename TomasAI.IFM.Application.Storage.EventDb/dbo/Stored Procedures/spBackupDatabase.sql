-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spBackupDatabase]
	-- Add the parameters for the stored procedure here
	@backupType char(4)
AS
BEGIN
	if @backupType = 'full'
		BACKUP DATABASE [eventdb] TO  disk = 'e:\backup\eventdb-full.bak' WITH  FORMAT, INIT, SKIP, NOREWIND, NOUNLOAD,  STATS = 10
	else
		BACKUP DATABASE [eventdb] TO  disk = 'e:\backup\eventdb-diff.bak' WITH  DIFFERENTIAL , FORMAT, INIT,  SKIP, NOREWIND, NOUNLOAD,  STATS = 10
END