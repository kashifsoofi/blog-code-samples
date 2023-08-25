IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Movies' and xtype='U')
BEGIN
    CREATE TABLE Movies (
        Id          UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY,
        Title       VARCHAR(100)        NOT NULL,
        Director    VARCHAR(100)        NOT NULL,
        ReleaseDate DateTime2           NOT NULL,
        TicketPrice DECIMAL(12, 4)      NOT NULL,
        CreatedAt   DateTime2           NOT NULL,
        UpdatedAt   DateTime2           NOT NULL
    )
END