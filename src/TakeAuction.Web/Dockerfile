# Build context is the repository root, matching the API image.
FROM node:22-alpine AS build
WORKDIR /app

COPY src/TakeAuction.Web/package.json src/TakeAuction.Web/package-lock.json ./
RUN npm ci

COPY src/TakeAuction.Web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime

# The SPA owns its own routing, so anything that is not a real file has to fall through to
# index.html or a deep link like /auctions/{id} would 404 on refresh.
COPY infra/nginx/web.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
