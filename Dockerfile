FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["WeatherBot2/WeatherBot2.csproj", "WeatherBot2/"]
RUN dotnet restore "WeatherBot2/WeatherBot2.csproj"
COPY . .
WORKDIR "/src/WeatherBot2"
RUN dotnet build "WeatherBot2.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WeatherBot2.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WeatherBot2.dll"]
