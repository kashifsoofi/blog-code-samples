# Integration Test Postgres with testcontainers-dotnet
This is a continuation of an earlier post [Integration Testing Postgres Store](https://kashifsoofi.github.io/aspnetcore/testing/integrationtest/postgres/postgres-store-integration-test/). In this tutorial I will extend the sample to use [testcontainers-dotnet](https://github.com/testcontainers/testcontainers-dotnet) to spin up database container and apply migrations before executing our integration tests.

Prior to this sample, pre-requisite of running integration tests was that database server is running either on machine or in a container and migrations are applied. This step removes that manual step.



## References
In no particular order  
* [REST API with ASP.NET Core 7 and Postgres](https://kashifsoofi.github.io/aspnetcore/rest/postgres/restapi-with-asp.net-core-7-and-postgres/)
* [Postgres Database](https://www.postgresql.org/)
* [Integration Testing](https://en.wikipedia.org/wiki/Integration_testing)
* [Dapper](https://github.com/DapperLib/Dapper)
* [Npgsql](https://www.npgsql.org/doc/index.html)
* [AutoFixture](https://github.com/AutoFixture/AutoFixture)
* [FluentAssertions](https://fluentassertions.com/)
* [Docker](https://www.docker.com/)
* [testcontainers-dotnet](https://github.com/testcontainers/testcontainers-dotnet)
* And many more