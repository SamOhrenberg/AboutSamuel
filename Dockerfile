# ── Build stage ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY Backend/PortfolioWebsite.Common/PortfolioWebsite.Common.csproj Backend/PortfolioWebsite.Common/
COPY Backend/PortfolioWebsite.Api/PortfolioWebsite.Api.csproj Backend/PortfolioWebsite.Api/
RUN dotnet restore Backend/PortfolioWebsite.Api/PortfolioWebsite.Api.csproj

# Copy everything and publish
COPY Backend/ Backend/
RUN dotnet publish Backend/PortfolioWebsite.Api/PortfolioWebsite.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# ── Runtime stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Railway sets PORT at runtime. This shell form allows variable expansion.
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

# Shell form so $PORT is expanded at runtime, not build time
CMD ASPNETCORE_URLS="http://+:${PORT:-5000}" dotnet PortfolioWebsite.Api.dll
