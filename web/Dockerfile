FROM node:22-alpine AS editor-build
WORKDIR /src/editor
COPY editor/package.json editor/package-lock.json editor/.npmrc ./
RUN npm ci
COPY editor/ ./
RUN npm run build

FROM node:22-alpine AS image-tools-build
WORKDIR /src/image-tools
COPY image-tools/package.json image-tools/package-lock.json ./
RUN npm ci
COPY image-tools/ ./
COPY extensions/lup/crop.mjs extensions/lup/crop.css /src/extensions/lup/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Mercantec.Tech.Contracts.csproj shared/
COPY web/Mercantec.Tech.Web.csproj web/
RUN dotnet restore web/Mercantec.Tech.Web.csproj
COPY shared/ shared/
COPY web/ web/
COPY --from=editor-build /src/web/wwwroot/notion-editor/ web/wwwroot/notion-editor/
COPY --from=image-tools-build /src/web/wwwroot/image-tools/ web/wwwroot/image-tools/
WORKDIR /src/web
RUN dotnet publish -c Release -o /app/publish

FROM nginx:1.27-alpine AS final
COPY web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 3000
CMD ["nginx", "-g", "daemon off;"]
