FROM node:22-alpine AS build
WORKDIR /app

COPY portal/package.json portal/package-lock.json ./
RUN npm ci

COPY portal/. .
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY portal/package.json portal/package-lock.json ./
RUN npm ci --omit=dev

COPY --from=build /app/dist ./dist

ENV HOST=0.0.0.0
ENV PORT=8080
EXPOSE 8080

CMD ["node", "./dist/server/entry.mjs"]
