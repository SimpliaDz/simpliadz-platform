FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Build.targets ./
COPY src/ ./src/
RUN dotnet restore src/Simplia.Api/Simplia.Api.csproj
RUN dotnet restore src/Simplia.DatabaseMigrator/Simplia.DatabaseMigrator.csproj
RUN dotnet publish src/Simplia.Api/Simplia.Api.csproj --no-restore -c Release -o /out/api
RUN dotnet publish src/Simplia.DatabaseMigrator/Simplia.DatabaseMigrator.csproj --no-restore -c Release -o /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Simplia.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrator
WORKDIR /app
COPY --from=build /out/migrator .
ENTRYPOINT ["dotnet", "Simplia.DatabaseMigrator.dll"]
