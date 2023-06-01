using System;
namespace Movies.Api.Configuration;

public class MoviesStoreConfiguration
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string MoviesCollectionName { get; set; } = null!;
}