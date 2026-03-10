# ----------- BUILD STAGE -----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first for better layer caching
COPY src/Api/Api.csproj src/Api/
COPY src/Application/Application.csproj src/Application/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/

# Restore dependencies
RUN dotnet restore src/Api/Api.csproj

# Copy the entire source
COPY src ./src

# Build
WORKDIR /src/src/Api
RUN dotnet build Api.csproj -c Release -o /app/build

# Publish
RUN dotnet publish Api.csproj -c Release -o /app/publish /p:UseAppHost=false


# ----------- RUNTIME STAGE -----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Expose API port
EXPOSE 8080

# Run the API
ENTRYPOINT ["dotnet", "Api.dll"]