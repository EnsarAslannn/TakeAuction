FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/TakeAuction.Api/TakeAuction.Api.csproj src/TakeAuction.Api/
RUN dotnet restore src/TakeAuction.Api/TakeAuction.Api.csproj

COPY src/TakeAuction.Api/ src/TakeAuction.Api/
RUN dotnet publish src/TakeAuction.Api/TakeAuction.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

RUN mkdir -p /app/App_Data/uploads/auctions \
    && chown -R $APP_UID:$APP_UID /app/App_Data

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "TakeAuction.Api.dll"]
