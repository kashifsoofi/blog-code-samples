UPDATE movies
SET
    title = @title,
    director = @director,
    release_date = @release_date,
    ticket_price = @ticket_price,
    updated_at = @updated_at
WHERE id = @id