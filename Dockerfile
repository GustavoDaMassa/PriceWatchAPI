FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copia apenas os .csproj primeiro para aproveitar cache de restore
COPY PriceWatch.sln .
COPY src/PriceWatch.Domain/PriceWatch.Domain.csproj             src/PriceWatch.Domain/
COPY src/PriceWatch.Application/PriceWatch.Application.csproj   src/PriceWatch.Application/
COPY src/PriceWatch.Infrastructure/PriceWatch.Infrastructure.csproj src/PriceWatch.Infrastructure/
COPY src/PriceWatch.API/PriceWatch.API.csproj                   src/PriceWatch.API/

RUN dotnet restore src/PriceWatch.API/PriceWatch.API.csproj

COPY src/ src/

RUN dotnet publish src/PriceWatch.API/PriceWatch.API.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PriceWatch.API.dll"]
