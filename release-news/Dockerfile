FROM mcr.microsoft.com/playwright:v1.64.1-noble
USER root
RUN git --version
WORKDIR /app
COPY release-news/package.json release-news/package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts
COPY release-news/ ./
USER pwuser
ENV NODE_ENV=production
ENV HOME=/tmp/lup-release-news
ENV XDG_CACHE_HOME=/tmp/lup-release-news/cache
ENV XDG_CONFIG_HOME=/tmp/lup-release-news/config
ENV XDG_RUNTIME_DIR=/tmp/lup-release-news/runtime
CMD ["node", "runner.mjs"]
