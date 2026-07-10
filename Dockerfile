# syntax=docker/dockerfile:1

# --- Build stage -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the solution + non-test project files first so `restore` is cached
# independently of source changes. The test project is intentionally omitted.
COPY TuyaHub.slnx ./
COPY TuyaHub/TuyaHub.csproj TuyaHub/
COPY TuyaHub.Application/TuyaHub.Application.csproj TuyaHub.Application/
COPY TuyaHub.Domain/TuyaHub.Domain.csproj TuyaHub.Domain/
COPY TuyaHub.Infrastructure/TuyaHub.Infrastructure.csproj TuyaHub.Infrastructure/
RUN dotnet restore TuyaHub/TuyaHub.csproj

# Copy the rest of the sources and publish the host only (never TuyaHub.Tests).
COPY . .
RUN dotnet publish TuyaHub/TuyaHub.csproj -c Release -o /app --no-restore

# --- Runtime stage -----------------------------------------------------------
# runtime (not aspnet): the host is a Generic-Host worker with no web server.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

ENV DOTNET_ENVIRONMENT=Production

# Run as the non-root user shipped in the .NET runtime image.
USER app

ENTRYPOINT ["dotnet", "TuyaHub.dll"]
