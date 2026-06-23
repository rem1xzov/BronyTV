# syntax=docker/dockerfile:1

# Контекст сборки: корень монорепозитория (см. docker-compose: context: .)

FROM node:20-alpine AS build
WORKDIR /app

COPY Brony_Video-main/package.json Brony_Video-main/package-lock.json* ./
RUN npm install

COPY Brony_Video-main/public ./public
COPY Brony_Video-main/src ./src

ARG REACT_APP_API_BASE_URL=/api
ENV REACT_APP_API_BASE_URL=$REACT_APP_API_BASE_URL

RUN npm run build

FROM nginx:1.27-alpine AS final
COPY Brony_Video-main/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/build /usr/share/nginx/html
EXPOSE 80
