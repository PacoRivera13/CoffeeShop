# Build context is the repository root: docker build -f src/Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so the layer is reused until a dependency changes.
COPY Directory.Build.props dotnet-tools.json ./
COPY src/Domain/CoffeeShop.Domain.csproj src/Domain/
COPY src/Application/CoffeeShop.Application.csproj src/Application/
COPY src/Infrastructure/CoffeeShop.Infrastructure.csproj src/Infrastructure/
COPY src/Api/CoffeeShop.Api.csproj src/Api/
RUN dotnet restore src/Api/CoffeeShop.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/CoffeeShop.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CoffeeShop.Api.dll"]
