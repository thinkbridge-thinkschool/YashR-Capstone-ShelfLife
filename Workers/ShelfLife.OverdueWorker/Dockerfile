FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ShelfLife.slnx Directory.Build.props ./
COPY src/ src/
COPY Workers/ Workers/

RUN dotnet publish Workers/ShelfLife.OverdueWorker/ShelfLife.OverdueWorker.csproj \
    --configuration Release \
    --output /app/publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ShelfLife.OverdueWorker.dll"]
