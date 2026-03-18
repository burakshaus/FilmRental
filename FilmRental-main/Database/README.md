# FilmRentalDb Database Setup

## Requirements
- Docker must be installed

## Setup Steps

### 1. Create Docker Container
```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=S1stemAdmin2024!" -p 1433:1433 --name film-rental-db -d mcr.microsoft.com/azure-sql-edge
```

### 2. Copy Database Files to Container
```bash
docker cp FilmRentalDb.mdf film-rental-db:/var/opt/mssql/data/FilmRentalDb.mdf
docker cp FilmRentalDb_log.ldf film-rental-db:/var/opt/mssql/data/FilmRentalDb_log.ldf
```

### 3. Attach the Database
Connect using SQL Server Management Studio or sqlcmd and run the following command:

```sql
CREATE DATABASE [FilmRentalDb]
ON (FILENAME = '/var/opt/mssql/data/FilmRentalDb.mdf'),
   (FILENAME = '/var/opt/mssql/data/FilmRentalDb_log.ldf')
FOR ATTACH;
```

### Connection Details
- **Server:** `localhost,1433`
- **User:** `sa`
- **Password:** `S1stemAdmin2024!`
- **Database:** `FilmRentalDb`
