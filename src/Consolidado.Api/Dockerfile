FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FluxoCaixa.sln .
COPY src/Domain src/Domain
COPY src/Application src/Application
COPY src/Infrastructure src/Infrastructure
COPY src/Consolidado.Api src/Consolidado.Api
RUN dotnet restore src/Consolidado.Api/Consolidado.Api.csproj
RUN dotnet publish src/Consolidado.Api/Consolidado.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Consolidado.Api.dll"]
