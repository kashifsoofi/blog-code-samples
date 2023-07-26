﻿using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Movies.Api.Store;
using Movies.Api.Store.SqlServer;
using Movies.Api.Tests.Integration.Helpers;

namespace Movies.Api.Tests.Integration.Store.SqlServer;

[Collection("DatabaseCollection")]
public class SqlServerMoviesStoreTests : IAsyncLifetime
{
    private readonly MoviesDatabaseHelper moviesDatabaseHelper;

    private readonly SqlServerMoviesStore sut;

    public SqlServerMoviesStoreTests(DatabaseFixture databaseFixture)
	{
        moviesDatabaseHelper = new MoviesDatabaseHelper(databaseFixture.ConnectionString);

        var myConfiguration = new Dictionary<string, string?>
        {
            {"ConnectionStrings:MoviesDb", databaseFixture.ConnectionString},
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        sut = new SqlServerMoviesStore(configuration);
	}

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await moviesDatabaseHelper.CleanTableAsync();
    }

    [Theory]
    [AutoData]
    public async void GetById_GivenRecordDoesNotExist_ShouldReturnNull(Guid id)
    {
        // Arrange
        // Act
        var result = await sut.GetById(id);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [AutoData]
    public async void GetById_GivenRecordExists_ShouldReturnRecord(Movie movie)
    {
        // Arrange
        await moviesDatabaseHelper.AddRecordAsync(movie);

        // Act
        var result = await sut.GetById(movie.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(
            movie,
            x => x.Excluding(p => p.ReleaseDate).Excluding(p => p.CreatedAt).Excluding(p => p.UpdatedAt));
        result.ReleaseDate.Should().BeCloseTo(movie.ReleaseDate, TimeSpan.FromSeconds(1));
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        result.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async void GetAll_GivenNoRecords_ShouldReturnEmptyCollection()
    {
        // Arrange
        // Act
        var result = await this.sut.GetAll();

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [AutoData]
    public async void GetAll_GivenRecordsExist_ShouldReturnCollection(List<Movie> movies)
    {
        // Arrange
        await moviesDatabaseHelper.AddRecordsAsync(movies);

        // Act
        var result = await this.sut.GetAll();

        // Assert
        result.Should().BeEquivalentTo(movies, x => x.Excluding(p => p.ReleaseDate).Excluding(p => p.CreatedAt).Excluding(p => p.UpdatedAt));
    }

    [Theory]
    [AutoData]
    public async void Create_GivenRecordDoesNotExist_ShouldCreateRecord(CreateMovieParams createMovieParams)
    {
        // Arrange
        // Act
        await sut.Create(createMovieParams);
        moviesDatabaseHelper.TrackId(createMovieParams.Id);

        // Assert
        var createdMovie = await moviesDatabaseHelper.GetRecordAsync(createMovieParams.Id);

        createdMovie.Should().BeEquivalentTo(createMovieParams, x => x.Excluding(p => p.ReleaseDate));
        createdMovie.ReleaseDate.Should().BeCloseTo(createMovieParams.ReleaseDate, TimeSpan.FromSeconds(1));
        createdMovie.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        createdMovie.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [AutoData]
    public async void Create_GivenRecordWithIdExists_ShouldThrowDuplicateKeyException(Movie movie)
    {
        // Arrange
        await moviesDatabaseHelper.AddRecordAsync(movie);

        var createMovieParams = new CreateMovieParams(movie.Id, movie.Title, movie.Director, movie.TicketPrice, movie.ReleaseDate);

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateKeyException>(async () => await sut.Create(createMovieParams));
    }

    [Theory]
    [AutoData]
    public async void Update_GivenRecordExists_ShouldUpdateRecord(Movie movie, UpdateMovieParams updateMovieParams)
    {
        // Arrange
        await moviesDatabaseHelper.AddRecordAsync(movie);

        // Act
        await sut.Update(movie.Id, updateMovieParams);

        // Assert
        var saved = await moviesDatabaseHelper.GetRecordAsync(movie.Id);

        saved.Should().BeEquivalentTo(updateMovieParams, x => x.Excluding(p => p.ReleaseDate));
        saved.ReleaseDate.Should().BeCloseTo(updateMovieParams.ReleaseDate, TimeSpan.FromSeconds(1));
        saved.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [AutoData]
    public async void Delete_GivenRecordExists_ShouldDeleteRecord(Movie movie)
    {
        // Arrange
        await moviesDatabaseHelper.AddRecordAsync(movie);

        // Act
        await sut.Delete(movie.Id);

        // Assert
        var loaded = await moviesDatabaseHelper.GetRecordAsync(movie.Id);
        loaded.Should().BeNull();
    }
}
