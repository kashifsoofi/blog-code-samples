using System;
using Dapper;
using System.Data;
using Microsoft.Data.SqlClient;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Movies.Api.Store.SqlServer;

public class SqlServerMoviesStore : IMoviesStore
{
    private readonly string connectionString;
    private readonly SqlHelper<SqlServerMoviesStore> sqlHelper;

    public SqlServerMoviesStore(IConfiguration configuration)
	{
        var connectionString = configuration.GetConnectionString("MoviesDb");
        if (connectionString == null)
        {
            throw new InvalidOperationException("Missing [MoviesDb] connection string.");
        }

        this.connectionString = connectionString;
        sqlHelper = new SqlHelper<SqlServerMoviesStore>();
    }

    public async Task Create(CreateMovieParams createMovieParams)
    {
        await using var connection = new SqlConnection(this.connectionString);
        {
            var parameters = new
            {
                createMovieParams.Id,
                createMovieParams.Title,
                createMovieParams.Director,
                createMovieParams.ReleaseDate,
                createMovieParams.TicketPrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            try
            {
                await connection.ExecuteAsync(
                    this.sqlHelper.GetSqlFromEmbeddedResource("Create"),
                    parameters,
                    commandType: CommandType.Text);
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627)
                {
                    throw new DuplicateKeyException();
                }

                throw;
            }
        }
    }

    public async Task Delete(Guid id)
    {
        await using var connection = new SqlConnection(this.connectionString);
        await connection.ExecuteAsync(
            sqlHelper.GetSqlFromEmbeddedResource("Delete"),
            new { id },
            commandType: CommandType.Text
            );
    }

    public async Task<IEnumerable<Movie>> GetAll()
    {
        await using var connection = new SqlConnection(this.connectionString);
        return await connection.QueryAsync<Movie>(
            sqlHelper.GetSqlFromEmbeddedResource("GetAll"),
            commandType: CommandType.Text
            );
    }

    public async Task<Movie?> GetById(Guid id)
    {
        await using var connection = new SqlConnection(this.connectionString);
        return await connection.QueryFirstOrDefaultAsync<Movie?>(
            sqlHelper.GetSqlFromEmbeddedResource("GetById"),
            new { id },
            commandType: System.Data.CommandType.Text
            );
    }

    public async Task Update(Guid id, UpdateMovieParams updateMovieParams)
    {
        await using var connection = new SqlConnection(this.connectionString);
        {
            var parameters = new
            {
                Id = id,
                updateMovieParams.Title,
                updateMovieParams.Director,
                updateMovieParams.ReleaseDate,
                updateMovieParams.TicketPrice,
                UpdatedAt = DateTime.UtcNow,
            };

            await connection.ExecuteAsync(
                this.sqlHelper.GetSqlFromEmbeddedResource("Update"),
                parameters,
                commandType: CommandType.Text);
        }
    }
}