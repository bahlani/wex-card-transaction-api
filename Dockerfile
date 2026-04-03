# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (for layer caching)
COPY CardTransactionApi.sln ./
COPY src/CardTransactionApi/CardTransactionApi.csproj src/CardTransactionApi/
COPY tests/CardTransactionApi.Tests/CardTransactionApi.Tests.csproj tests/CardTransactionApi.Tests/
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish src/CardTransactionApi/CardTransactionApi.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a directory for the SQLite database
RUN mkdir -p /app/data

COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/cardtransactions.db"

EXPOSE 8080

ENTRYPOINT ["dotnet", "CardTransactionApi.dll"]
