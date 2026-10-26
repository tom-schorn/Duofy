# Das Image der Duofy-API (Spec 79). Gebaut aus dem Wurzelordner des Repositorys:
#   docker build -f Backend/Duofy.Backend/Dockerfile --build-arg VERSION=0.3.0 --build-arg BUILD_DATE=2026-10-26T12:00:00Z .
# Im Betrieb baut es deploy/docker-compose.yml.

# --- Bauen: mit dem SDK, das nicht ins fertige Image kommt ------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

# Erst nur die Projektdateien: Solange sie gleich bleiben, nimmt Docker die Pakete aus dem Cache.
COPY Directory.Build.props ./
COPY Backend/Duofy.Schemas/Duofy.Schemas.csproj Backend/Duofy.Schemas/
COPY Backend/Duofy.Backend/Duofy.Backend.csproj Backend/Duofy.Backend/
RUN dotnet restore Backend/Duofy.Backend/Duofy.Backend.csproj

COPY Backend/Duofy.Schemas/ Backend/Duofy.Schemas/
COPY Backend/Duofy.Backend/ Backend/Duofy.Backend/
RUN dotnet publish Backend/Duofy.Backend/Duofy.Backend.csproj --configuration Release --no-restore --output /app

# --- Laufen: nur die ASP.NET-Laufzeit -------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime

ARG VERSION=dev
ARG BUILD_DATE=unknown
LABEL org.opencontainers.image.title="Duofy API" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.licenses="MIT"

# curl nur für den HEALTHCHECK; die Laufzeit bringt keins mit.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Der Ordner der Schlüssel (Duofy__KeysDirectory) gehört dem Benutzer app, damit das Volume darüber ihm gehört.
RUN mkdir /keys && chown "$APP_UID" /keys

WORKDIR /app
COPY --from=build /app ./

# Nicht als root: Das Image bringt den Benutzer app mit (APP_UID).
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Duofy__KeysDirectory=/keys
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "Duofy.Backend.dll"]
