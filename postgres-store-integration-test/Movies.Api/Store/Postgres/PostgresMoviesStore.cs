using System;
using Npgsql;
using Dapper;
using System.Data;

namespace Movies.Api.Store.Postgres;

public class PostgresMoviesStore : IMoviesStore
{
    private readonly string connectionString;
    private readonly SqlHelper<PostgresMoviesStore> sqlHelper;

    public PostgresMoviesStore(IConfiguration configuration)
	{
        var connectionString = configuration.GetConnectionString("MoviesDb");
        if (connectionString == null)
        {
            throw new InvalidOperationException("Missing [MoviesDb] connection string.");
        }

        this.connectionString = connectionString;
        sqlHelper = new SqlHelper<PostgresMoviesStore>();
    }

    public async Task Create(CreateMovieParams createMovieParams)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        {
            var parameters = new
            {
                id = createMovieParams.Id,
                title = createMovieParams.Title,
                director = createMovieParams.Director,
                release_date = createMovieParams.ReleaseDate,
                ticket_price = createMovieParams.TicketPrice,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow,
            };

            try
            {
                await connection.ExecuteAsync(
                    this.sqlHelper.GetSqlFromEmbeddedResource("Create"),
                    parameters,
                    commandType: CommandType.Text);
            }
            catch (NpgsqlException ex)
            {
                if (ex.SqlState == "23505")
                {
                    throw new DuplicateKeyException();
                }

                throw;
            }
        }
    }

    public async Task Delete(Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(
            sqlHelper.GetSqlFromEmbeddedResource("Delete"),
            new { id },
            commandType: CommandType.Text
            );
    }

    public async Task<IEnumerable<Movie>> GetAll()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.QueryAsync<Movie>(
            sqlHelper.GetSqlFromEmbeddedResource("GetAll"),
            commandType: CommandType.Text
            );
    }

    public async Task<Movie?> GetById(Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.QueryFirstOrDefaultAsync<Movie?>(
            sqlHelper.GetSqlFromEmbeddedResource("GetById"),
            new { id },
            commandType: System.Data.CommandType.Text
            );
    }

    public async Task Update(Guid id, UpdateMovieParams updateMovieParams)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        {
            var parameters = new
            {
                id = id,
                title = updateMovieParams.Title,
                director = updateMovieParams.Director,
                release_date = updateMovieParams.ReleaseDate,
                ticket_price = updateMovieParams.TicketPrice,
                updated_at = DateTime.UtcNow,
            };

            await connection.ExecuteAsync(
                this.sqlHelper.GetSqlFromEmbeddedResource("Update"),
                parameters,
                commandType: CommandType.Text);
        }
    }
}

