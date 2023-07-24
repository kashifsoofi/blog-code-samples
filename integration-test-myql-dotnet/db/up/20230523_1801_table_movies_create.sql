USE Movies;

CREATE TABLE IF NOT EXISTS Movies (
    Id          CHAR(36)        NOT NULL UNIQUE,
    Title       VARCHAR(100)    NOT NULL,
    Director    VARCHAR(100)    NOT NULL,
    ReleaseDate DATETIME        NOT NULL,
    TicketPrice DECIMAL(12, 4)  NOT NULL,
    CreatedAt   DATETIME        NOT NULL,
    UpdatedAt   DATETIME        NOT NULL,
    PRIMARY KEY (Id)
) ENGINE=INNODB;