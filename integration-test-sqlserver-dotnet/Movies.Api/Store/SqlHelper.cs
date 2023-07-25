using System;
namespace Movies.Api.Store;

public class SqlHelper<T>
{
    public string GetSqlFromEmbeddedResource(string name)
    {
        using var resourceStream = typeof(T).Assembly.GetManifestResourceStream(typeof(T).Namespace + ".Sql." + name + ".sql");
        using var reader = new StreamReader(resourceStream!);
        return reader.ReadToEnd();
    }
}