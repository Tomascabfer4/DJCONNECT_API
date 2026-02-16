# 1. Imagen de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["API_DJCONNECT.csproj", "./"]
RUN dotnet restore "API_DJCONNECT.csproj"
COPY . .
RUN dotnet publish "API_DJCONNECT.csproj" -c Release -o /app/publish

# 2. Imagen de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 3. Puerto 8080 (Estándar de Render)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "API_DJCONNECT.dll"]