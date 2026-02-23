# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DenoLite.Api/DenoLite.Api.csproj", "DenoLite.Api/"]
COPY ["DenoLite.Application/DenoLite.Application.csproj", "DenoLite.Application/"]
COPY ["DenoLite.Domain/DenoLite.Domain.csproj", "DenoLite.Domain/"]
COPY ["DenoLite.Infrastructure/DenoLite.Infrastructure.csproj", "DenoLite.Infrastructure/"]

RUN dotnet restore "DenoLite.Api/DenoLite.Api.csproj"
COPY . .
RUN dotnet publish "DenoLite.Api/DenoLite.Api.csproj" -c Release -o /app/publish --no-restore

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DenoLite.Api.dll"]
