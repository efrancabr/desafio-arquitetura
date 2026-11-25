FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FluxoCaixa.sln .
COPY src/Domain src/Domain
COPY src/Application src/Application
COPY src/Infrastructure src/Infrastructure
COPY src/DbInit src/DbInit
RUN dotnet restore src/DbInit/DbInit.csproj
RUN dotnet publish src/DbInit/DbInit.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "DbInit.dll"]
