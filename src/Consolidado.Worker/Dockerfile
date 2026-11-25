FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FluxoCaixa.sln .
COPY src/Domain src/Domain
COPY src/Application src/Application
COPY src/Infrastructure src/Infrastructure
COPY src/Consolidado.Worker src/Consolidado.Worker
RUN dotnet restore src/Consolidado.Worker/Consolidado.Worker.csproj
RUN dotnet publish src/Consolidado.Worker/Consolidado.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Consolidado.Worker.dll"]
