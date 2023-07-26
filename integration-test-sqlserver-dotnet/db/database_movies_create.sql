USE master

IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = 'Movies')
BEGIN
    CREATE DATABASE Movies
    SELECT 1
END