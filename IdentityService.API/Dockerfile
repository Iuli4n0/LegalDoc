# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything, .dockerignore keeps out bin/obj
COPY . .

# Restore specifically for this project
RUN dotnet restore "IdentityService.API/IdentityService.API.csproj"

# Build and publish
WORKDIR /src/IdentityService.API
RUN dotnet publish -c Release -o /app/publish 

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "IdentityService.API.dll"]
