# Build context is the repository root - see backend/src/FlowForge.API/Dockerfile for why.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/ backend/
RUN dotnet restore backend/FlowForge.sln
RUN dotnet publish backend/src/FlowForge.Workers/FlowForge.Workers.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FlowForge.Workers.dll"]
