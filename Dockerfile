# Use the official .NET 8.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory
WORKDIR /src

# Copy project file and restore dependencies
COPY OsmGursBuildingImport/OsmGursBuildingImport.csproj .
COPY OsmGursBuildingImport/NuGet.config .
RUN dotnet restore

# Copy the source code
COPY OsmGursBuildingImport/ .

# Build the application
RUN dotnet build -c Release --no-restore

# Publish the application
RUN dotnet publish -c Release --no-build -o /app/publish

# Use the official ASP.NET Core 8.0 runtime for the final image (needed for Web SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Install dependencies for osmconvert
RUN apt-get update && \
    apt-get install -y wget && \
    # Download and install osmconvert
    wget -O /usr/local/bin/osmconvert http://m.m.i24.cc/osmconvert64 && \
    chmod +x /usr/local/bin/osmconvert && \
    # Clean up
    apt-get remove -y wget && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

# Set the working directory
WORKDIR /app

# Copy Overrides folder
COPY overrides ./overrides
# Copy the published application
COPY --from=build /app/publish .

# Create a non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

# Set the entry point
ENTRYPOINT ["dotnet", "OsmGursBuildingImport.dll"]
