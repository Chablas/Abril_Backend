# Imagen base del runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Imagen del SDK para build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Abril-Backend.csproj", "./"]
RUN dotnet restore "Abril-Backend.csproj"
COPY . .
RUN dotnet publish "Abril-Backend.csproj" -c Release -o /app/publish

# Imagen final
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Abril-Backend.dll"]