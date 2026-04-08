# ═══════════════════════════════════════════════════════════════
# STAGE 1: BUILD - Compile và Publish .NET Application
# ═══════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project file
COPY ["unicheck_backend.csproj", "."]

# Restore dependencies
RUN dotnet restore "unicheck_backend.csproj"

# Copy source code
COPY . .

# Build và publish
RUN dotnet publish "unicheck_backend.csproj" -c Release -o /app/out

# ═══════════════════════════════════════════════════════════════
# STAGE 2: RUNTIME - Runtime image (nhỏ hơn)
# ═══════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/out .

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "unicheck_backend.dll"]
