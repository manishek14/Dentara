# ─── Build stage ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy only project files first for better layer caching
COPY src/Dentara.Domain/Dentara.Domain.csproj src/Dentara.Domain/
COPY src/Dentara.Application/Dentara.Application.csproj src/Dentara.Application/
COPY src/Dentara.Infrastructure/Dentara.Infrastructure.csproj src/Dentara.Infrastructure/
COPY src/Dentara.Api/Dentara.Api.csproj src/Dentara.Api/
COPY Dentara.sln .
COPY Directory.Build.props .

# Restore packages
RUN dotnet restore

# Copy all source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/Dentara.Api/Dentara.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ─── Runtime stage ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Install SQL Server tools (optional — for debugging connectivity)
# RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Expose HTTP port (container internally uses 8080)
EXPOSE 8080

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Dentara.Api.dll"]
