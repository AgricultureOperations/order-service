# ------------------------------
# Stage 1: Build
# ------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and restore dependencies
COPY *.sln .
COPY src/Api/*.csproj ./src/Api/
COPY src/Application/*.csproj ./src/Application/
COPY src/Domain/*.csproj ./src/Domain/
COPY src/Infrastructure/*.csproj ./src/Infrastructure/

RUN dotnet restore

# Copy all source code
COPY src/ ./src/

# Build and publish
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ------------------------------
# Stage 2: Runtime
# ------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser
USER appuser

WORKDIR /app
COPY --from=build /app/publish .

# Expose port
EXPOSE 80

# Start the app
ENTRYPOINT ["dotnet", "Api.dll"]