
# ── Runtime base image ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN mkdir -p /data /app/IdentitySuite /app/Certificates \
    && chown -R $APP_UID:$APP_UID /data /app
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["nuget.config", "."]
COPY ["local-packages/", "local-packages/"]
COPY ["IdentitySuite.Linux/IdentitySuite.Linux.csproj", "IdentitySuite.Linux/"]
RUN dotnet restore "./IdentitySuite.Linux/IdentitySuite.Linux.csproj"

COPY . .
WORKDIR "/src/IdentitySuite.Linux"
RUN dotnet build "./IdentitySuite.Linux.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Publish stage ─────────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./IdentitySuite.Linux.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ── Final image ───────────────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "IdentitySuite.Linux.dll"]
        