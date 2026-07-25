# Этап 1: Сборка React приложения
FROM node:20-alpine AS build
WORKDIR /app

# Копируем зависимости из текущей директории
COPY package.json package-lock.json* ./
RUN npm install

# Копируем весь код проекта
COPY public ./public
COPY src ./src

ARG REACT_APP_API_BASE_URL=/api
ENV REACT_APP_API_BASE_URL=$REACT_APP_API_BASE_URL
ENV GENERATE_SOURCEMAP=false

RUN npm run build

# Этап 2: Раздача статики через Nginx
FROM nginx:1.27-alpine AS final

# Копируем конфиг Nginx и собранный билд
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/build /usr/share/nginx/html

EXPOSE 80
