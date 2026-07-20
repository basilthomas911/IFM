ALTER ROLE [db_datareader] ADD MEMBER [DEV-SERVER\IFMApp];


GO
ALTER ROLE [db_datareader] ADD MEMBER [basilt];


GO
ALTER ROLE [db_datareader] ADD MEMBER [NT SERVICE\SQLAgent$DOMAINDATA];


GO
ALTER ROLE [db_datawriter] ADD MEMBER [DEV-SERVER\IFMApp];


GO
ALTER ROLE [db_datawriter] ADD MEMBER [basilt];


GO
ALTER ROLE [db_datawriter] ADD MEMBER [NT SERVICE\SQLAgent$DOMAINDATA];

