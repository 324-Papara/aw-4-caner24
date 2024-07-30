
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Para.Api/Para.Api.csproj", "Para.Api/"]
COPY ["Para.Base/Para.Base.csproj", "Para.Base/"]
COPY ["Para.Bussiness/Para.Bussiness.csproj", "Para.Bussiness/"]
COPY ["Para.Data/Para.Data.csproj", "Para.Data/"]
COPY ["Para.Schema/Para.Schema.csproj", "Para.Schema/"]
RUN dotnet restore "./Para.Api/Para.Api.csproj"
COPY . .
WORKDIR "/src/Para.Api"
RUN dotnet build "./Para.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Para.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Para.Api.dll"]