# syntax=docker/dockerfile:1

# Build context: repository root (see docker-compose: ai-build context: .)

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY AiBronyTV/ ./
WORKDIR /src/AiBronyTV
RUN dotnet restore "AiBronyTV.csproj"
RUN dotnet publish "AiBronyTV.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AiBronyTV.dll"]
