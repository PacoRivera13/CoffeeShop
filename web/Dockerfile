# Builds the Angular app and serves it with nginx, which also proxies /api to the api service.
FROM node:26.13.1-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
# Install scripts stay blocked as in development (DECISIONS.md); the prebuilt platform binaries are enough.
RUN npm ci --ignore-scripts --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.29.3-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/web/browser /usr/share/nginx/html
EXPOSE 8080
