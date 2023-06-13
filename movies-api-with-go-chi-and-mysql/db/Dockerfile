FROM migrate/migrate

# Copy all db files
COPY ./migrations /migrations

ENTRYPOINT [ "migrate", "-path", "/migrations", "-database"]
CMD ["mysql://root:Password123@tcp(movies.db:3306)/moviesdb up"]