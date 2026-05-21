# ──────────────────────────────────────────────────────────────────────────────
# Stage 1 – Build
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore first (layer-cache friendly)
COPY src/HostedAgent/HostedAgent.csproj src/HostedAgent/
RUN dotnet restore src/HostedAgent/HostedAgent.csproj

# Copy the rest of the source and publish
COPY src/HostedAgent/ src/HostedAgent/
RUN dotnet publish src/HostedAgent/HostedAgent.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish

# ──────────────────────────────────────────────────────────────────────────────
# Stage 2 – Runtime
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system --gid 1001 agentgroup \
 && adduser  --system --uid 1001 --ingroup agentgroup agentuser
USER agentuser

COPY --from=build /app/publish .

# Port the API listens on
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "HostedAgent.dll"]
