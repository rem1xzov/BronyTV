# syntax=docker/dockerfile:1

# Файл в каталоге BronyTV (рядом с BronyTV.sln). Контекст сборки в compose: ./BronyTV

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["BronyTV.sln", "./"]
COPY ["BronyTV/BronyTV.csproj", "BronyTV/"]
RUN dotnet restore "BronyTV/BronyTV.csproj"

COPY BronyTV/ BronyTV/
WORKDIR /src/BronyTV
RUN dotnet publish "BronyTV.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN mkdir -p /app/wwwroot/content

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "BronyTV.dll"]
