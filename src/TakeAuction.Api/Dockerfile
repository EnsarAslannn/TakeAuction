# Build context is the repository root so the project file can be restored on its own layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restoring before the sources are copied keeps the NuGet layer cached across code changes.
COPY src/TakeAuction.Api/TakeAuction.Api.csproj src/TakeAuction.Api/
RUN dotnet restore src/TakeAuction.Api/TakeAuction.Api.csproj

COPY src/TakeAuction.Api/ src/TakeAuction.Api/
RUN dotnet publish src/TakeAuction.Api/TakeAuction.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is here so the container can answer Compose's healthcheck; the aspnet image ships
# without any HTTP client of its own.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Seller uploads land here at runtime. Creating the directory with the app user's ownership
# before declaring the volume means Docker seeds a fresh named volume with those permissions
# rather than a root-owned one the process cannot write to.
RUN mkdir -p /app/App_Data/uploads/auctions \
    && chown -R $APP_UID:$APP_UID /app/App_Data

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "TakeAuction.Api.dll"]
