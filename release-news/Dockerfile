FROM mcr.microsoft.com/playwright:v1.64.1-noble
USER root
RUN git --version
WORKDIR /app
COPY release-news/package.json release-news/package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts
COPY release-news/ ./
USER pwuser
ENV NODE_ENV=production
CMD ["node", "runner.mjs"]
