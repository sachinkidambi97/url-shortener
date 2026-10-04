# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY UrlShortener.sln .
COPY UrlShortener.Api/UrlShortener.Api.csproj UrlShortener.Api/
COPY UrlShortener.Tests/UrlShortener.Tests.csproj UrlShortener.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish UrlShortener.Api/UrlShortener.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UrlShortener.Api.dll"]
