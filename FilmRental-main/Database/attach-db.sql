CREATE DATABASE [FilmRentalDb]
ON (FILENAME = '/var/opt/mssql/data/FilmRentalDb.mdf'),
   (FILENAME = '/var/opt/mssql/data/FilmRentalDb_log.ldf')
FOR ATTACH;
GO
