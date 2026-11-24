FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/SimpliaDz.sln
RUN dotnet publish src/SimpliaDz.Api/SimpliaDz.Api.csproj --no-restore -c Release -o /out/api
RUN dotnet publish src/SimpliaDz.DatabaseMigrator/SimpliaDz.DatabaseMigrator.csproj --no-restore -c Release -o /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SimpliaDz.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrator
WORKDIR /app
COPY --from=build /out/migrator .
ENTRYPOINT ["dotnet", "SimpliaDz.DatabaseMigrator.dll"]
