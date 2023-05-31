FROM mcr.microsoft.com/dotnet/sdk:3.1-alpine

ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet tool install -g dotnet-script --version 1.1.0
RUN dotnet tool install -g dotnet-roundhouse --version 1.3.1

WORKDIR /db

# Copy all db files
COPY . .

ENTRYPOINT ["dotnet-script", "wait-for-db.csx", "--", "rh", "--createdatabasescript", "database_movies_create.sql", "--silent", "-cs"]
CMD ["Server=movies.db;Database=master;User ID=sa;Password=Password123;"]