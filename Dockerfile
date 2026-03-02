# syntax=docker/dockerfile:1.4
# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first — this layer is cached until any .csproj changes
COPY src/TicketService.Domain/TicketService.Domain.csproj             src/TicketService.Domain/
COPY src/TicketService.Application/TicketService.Application.csproj   src/TicketService.Application/
COPY src/TicketService.Infrastructure/TicketService.Infrastructure.csproj src/TicketService.Infrastructure/
COPY src/TicketService.API/TicketService.API.csproj                   src/TicketService.API/

# Restore NuGet packages — cached by BuildKit unless .csproj files change.
# The NuGet cache mount persists across builds so packages are never re-downloaded.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/TicketService.API/TicketService.API.csproj

# Copy source code (obj/ and bin/ are excluded via .dockerignore)
COPY src/ src/

# Publish in Release mode — reuse the restored packages from the cache mount.
# --no-restore: restore was already done above; skip it to save time.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/TicketService.API/TicketService.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install libgssapi-krb5-2 required by Npgsql for PostgreSQL connections.
# The aspnet slim image omits this Kerberos/GSSAPI library by default.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Create a non-root user for security
RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose HTTP port (HTTPS is terminated at the reverse proxy / load balancer)
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "TicketService.API.dll"]