# Build context is the repository root (see docker-compose.yml), since the solution
# spans multiple projects under backend/src and backend/tests that all need to be
# present for `dotnet restore` to resolve project references.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/ backend/
RUN dotnet restore backend/FlowForge.sln
RUN dotnet publish backend/src/FlowForge.API/FlowForge.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FlowForge.API.dll"]
