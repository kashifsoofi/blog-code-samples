CREATE TABLE IF NOT EXISTS movies (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    title VARCHAR(100) NOT NULL,
    director VARCHAR(100) NOT NULL,
    release_date TIMESTAMP NOT NULL,
    ticket_price DECIMAL(12, 2) NOT NULL,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc') NOT NULL,
    updated_at TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc') NOT NULL
)