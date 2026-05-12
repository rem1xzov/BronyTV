# syntax=docker/dockerfile:1

# Контекст сборки: корень монорепозитория (см. docker-compose: context: .)

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BronyTV/ ./
WORKDIR /src/BronyTV
RUN dotnet restore "BronyTV.csproj"
RUN dotnet publish "BronyTV.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN mkdir -p /app/wwwroot/content

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "BronyTV.dll"]
