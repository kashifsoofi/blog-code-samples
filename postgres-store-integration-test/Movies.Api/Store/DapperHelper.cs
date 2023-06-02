using System;
namespace Movies.Api.Store;

public static class DapperHelper
{
    public static void Init()
    {
        // This does not work for constructor property mapping
        // Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}

