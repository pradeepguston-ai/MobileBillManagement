# Mobile Bill Management web front end: the React build served by nginx, which also forwards /api to the API.
# Build from this folder:  docker build -t mobilebill-web .

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Empty base URL: the browser calls /api on the same address, and nginx forwards it to the API container.
ENV VITE_API_BASE_URL=
RUN npm run build

# Runs nginx as a non-root user.
FROM nginxinc/nginx-unprivileged:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
