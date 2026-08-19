FROM node:26-alpine AS build
WORKDIR /app

COPY src/TakeAuction.Web/package.json src/TakeAuction.Web/package-lock.json ./
RUN npm ci

COPY src/TakeAuction.Web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime

COPY infra/nginx/web.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
