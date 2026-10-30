FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Mercantec.Tech.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Mercantec.Tech.Api.dll"]
