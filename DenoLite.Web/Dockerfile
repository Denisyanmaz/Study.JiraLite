# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DenoLite.Web/DenoLite.Web.csproj", "DenoLite.Web/"]
COPY ["DenoLite.Application/DenoLite.Application.csproj", "DenoLite.Application/"]
COPY ["DenoLite.Domain/DenoLite.Domain.csproj", "DenoLite.Domain/"]

RUN dotnet restore "DenoLite.Web/DenoLite.Web.csproj"
COPY . .
RUN dotnet publish "DenoLite.Web/DenoLite.Web.csproj" -c Release -o /app/publish --no-restore

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DenoLite.Web.dll"]
