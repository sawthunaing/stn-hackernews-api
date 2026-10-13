# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer stays cached until the project file changes
COPY src/Stn.HackerNews.Api/Stn.HackerNews.Api.csproj src/Stn.HackerNews.Api/
RUN dotnet restore src/Stn.HackerNews.Api/Stn.HackerNews.Api.csproj

COPY src/ src/
RUN dotnet publish src/Stn.HackerNews.Api/Stn.HackerNews.Api.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# .NET 8 images listen on port 8080 and include a non-root "app" user
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Stn.HackerNews.Api.dll"]