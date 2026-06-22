FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and all project files first so layer cache is reused on code-only changes
COPY ShelfLife.slnx Directory.Build.props ./
COPY src/ src/

RUN dotnet publish src/Host/ShelfLife.Api/ShelfLife.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ShelfLife.Api.dll"]
