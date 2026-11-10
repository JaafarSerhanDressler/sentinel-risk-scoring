# syntax=docker/dockerfile:1
# Portable container for the Sentinel Participant Risk Response App
# (ASP.NET Core API hosting the Blazor WebAssembly PWA on the same origin).
#
# Build:  docker build -t sentinel-risk .
# Run:    docker run -p 8080:8080 sentinel-risk   ->  http://localhost:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (copy csproj files first for layer caching)
COPY RiskScoring.slnx ./
COPY src/RiskScoring.Shared/RiskScoring.Shared.csproj src/RiskScoring.Shared/
COPY src/RiskScoring.Api/RiskScoring.Api.csproj src/RiskScoring.Api/
COPY src/RiskScoring.Client/RiskScoring.Client.csproj src/RiskScoring.Client/
RUN dotnet restore src/RiskScoring.Api/RiskScoring.Api.csproj

# Build & publish
COPY . .
RUN dotnet publish src/RiskScoring.Api/RiskScoring.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# Bind to the platform-provided $PORT (Render/Railway/etc.), default 8080 (Coolify/local).
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet RiskScoring.Api.dll"]
