IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Movies' and xtype='U')
BEGIN
    DROP TABLE Movies
END
