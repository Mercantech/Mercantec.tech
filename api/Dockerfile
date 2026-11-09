FROM node:22-alpine AS release-info
WORKDIR /src
COPY api/ api/
COPY web/ web/
COPY shared/ shared/
COPY editor/ editor/
COPY image-tools/ image-tools/
COPY extensions/ extensions/
COPY collaboration/ collaboration/
COPY release-news/ release-news/
COPY docker-compose.yml ./
RUN node release-news/fingerprint.mjs /src /release.json

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Mercantec.Tech.Contracts.csproj shared/
COPY api/Mercantec.Tech.Api.csproj api/
RUN dotnet restore api/Mercantec.Tech.Api.csproj
COPY shared/ shared/
COPY api/ api/
COPY web/Courses/CourseTemplateCatalog.cs web/Courses/
COPY web/Public/PublicCatalog.cs web/Public/
COPY web/Demo/DemoCatalog.cs web/Demo/
COPY web/Demo/LearningContent.cs web/Demo/
COPY web/Characters/CharacterCatalog.cs web/Characters/
COPY web/Characters/VatsonEmojiCatalog.cs web/Characters/
WORKDIR /src/api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mercantec.Tech.Api.dll"]

# Only the dedicated conversion service includes document/video tooling.
FROM base AS preview-tools
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    libreoffice-writer libreoffice-calc libreoffice-impress ffmpeg poppler-utils fonts-liberation fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /spool && chown $APP_UID /spool

FROM preview-tools AS preview-worker
COPY --from=build /app/publish .
USER $APP_UID
ENV PreviewConverter__Only=true

FROM base AS final
COPY --from=build /app/publish .

FROM final AS storage-worker
RUN mkdir -p /spool && chown $APP_UID /spool
USER $APP_UID
ENV PreviewWorker__Enabled=true
ENV PreviewWorker__Only=true

FROM final AS runtime
COPY --from=release-info /release.json /app/release.json
