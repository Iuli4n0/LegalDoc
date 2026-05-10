# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["LegalDoc.Frontend/LegalDoc.Frontend.csproj", "LegalDoc.Frontend/"]

RUN dotnet restore "LegalDoc.Frontend/LegalDoc.Frontend.csproj"

COPY LegalDoc.Frontend/ LegalDoc.Frontend/

WORKDIR /src/LegalDoc.Frontend
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080

ENTRYPOINT ["dotnet", "LegalDoc.Frontend.dll"]

