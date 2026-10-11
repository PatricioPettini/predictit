# Imagen del frontend.
#
# El resultado de `vite build` son archivos estáticos: no hace falta Node en el
# servidor. La imagen final es nginx con el `dist/` adentro, unos 50 MB contra
# los ~1,1 GB que pesaría la imagen de Node con `node_modules`.
#
# nginx además sirve de proxy hacia la API, que es lo que mantiene la URL del
# backend fuera del bundle: el frontend pide a `/api` y no sabe dónde vive el
# servidor. Es el mismo esquema que el proxy de Vite en desarrollo.

# ---------------------------------------------------------------- compilación
FROM node:22-alpine AS build
WORKDIR /app

# `npm ci` y no `npm install`: instala exactamente lo que dice el lock, falla si
# el lock no coincide con el package.json, y no lo modifica. Un build que puede
# cambiar sus propias dependencias no es reproducible.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# ------------------------------------------------------------------- runtime
FROM nginx:1.27-alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY cabeceras-de-seguridad.conf /etc/nginx/cabeceras-de-seguridad.conf

# La configuracion entra como plantilla: la imagen de nginx corre `envsubst`
# sobre `/etc/nginx/templates/` al arrancar y escribe el resultado en
# `/etc/nginx/conf.d/`. Es lo que deja que la misma imagen sirva en Docker
# Compose —donde la API es el servicio `api`— y en Fly.io, donde vive en otra
# aplicacion sobre la red privada.
COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# Sin el filtro, envsubst reemplaza **todas** las variables del archivo, y ahi
# adentro hay variables de nginx —`$uri`, `$host`, `$remote_addr`— que quedarian
# vacias: el proxy dejaria de pasar cabeceras y la SPA devolveria 404 al
# recargar. Solo se sustituye la unica que es del entorno.
ENV NGINX_ENVSUBST_FILTER="^API_UPSTREAM$"
ENV API_UPSTREAM="api:8080"

EXPOSE 80
