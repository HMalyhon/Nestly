# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
WORKDIR /src

# The lockfile alone first: npm ci is the slow step and only package.json can invalidate it.
COPY src/Nestly.Web/package.json src/Nestly.Web/package-lock.json ./
RUN npm ci

COPY src/Nestly.Web/ ./

# `npm run build` is lint, then typecheck, then bundle. The image cannot be built from code that
# would fail CI, which is the point of leaving the gate inside the script rather than beside it.
RUN npm run build

FROM nginx:1.29-alpine AS runtime

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html

# 8080 rather than 80, so nothing here needs to bind a privileged port.
EXPOSE 8080
