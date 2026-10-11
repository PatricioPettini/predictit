# Imagen de la API (PredictIT.Api).
#
# Multi-etapa a propósito: la imagen final lleva el runtime de ASP.NET y no el
# SDK. La imagen resultante mide 352 MB; una sola etapa sobre el SDK arrancaría
# en más de 1 GB y se llevaría adentro el compilador, los paquetes de NuGet y el
# código fuente, que en un servidor no hacen falta y sí amplían lo que encuentra
# quien entre.
#
# El contexto de build es `backend/`, porque la API referencia a los otros cinco
# proyectos y `dotnet restore` necesita verlos.

# ---------------------------------------------------------------- compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Primero sólo los .csproj y la solución, y después el resto del código.
#
# El orden importa: Docker cachea por capa, y restaurar paquetes es lo más lento
# del build. Copiando primero los archivos de proyecto, un cambio en el código
# —que es lo que cambia todo el tiempo— reutiliza la capa del restore. Copiando
# todo junto, cada cambio de una línea vuelve a bajar todos los paquetes.
COPY PredictIT.sln ./
COPY PredictIT.Domain/PredictIT.Domain.csproj   PredictIT.Domain/
COPY PredictIT.DAO/PredictIT.DAO.csproj         PredictIT.DAO/
COPY PredictIT.Service/PredictIT.Service.csproj PredictIT.Service/
COPY PredictIT.BLL/PredictIT.BLL.csproj         PredictIT.BLL/
COPY PredictIT.Api/PredictIT.Api.csproj         PredictIT.Api/
COPY PredictIT.Tests/PredictIT.Tests.csproj     PredictIT.Tests/

# El bloqueo de paquetes y el props que lo activa. Sin ellos la imagen resuelve
# las versiones por su cuenta y puede construirse con un arbol distinto del que
# se probo (OWASP A08).
COPY Directory.Build.props ./
COPY PredictIT.Domain/packages.lock.json   PredictIT.Domain/
COPY PredictIT.DAO/packages.lock.json      PredictIT.DAO/
COPY PredictIT.Service/packages.lock.json  PredictIT.Service/
COPY PredictIT.BLL/packages.lock.json      PredictIT.BLL/
COPY PredictIT.Api/packages.lock.json      PredictIT.Api/
COPY PredictIT.Tests/packages.lock.json    PredictIT.Tests/

RUN dotnet restore PredictIT.Api/PredictIT.Api.csproj --locked-mode

COPY . .

# `--no-restore` porque ya se restauró arriba: sin esto la capa cacheada no
# sirve de nada.
RUN dotnet publish PredictIT.Api/PredictIT.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app

# ------------------------------------------------------------------- runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# curl, unicamente para el healthcheck. La imagen de runtime de .NET no trae ni
# curl ni wget —correctamente: menos herramientas adentro es menos superficie—,
# asi que un HEALTHCHECK que las use falla siempre y el contenedor queda en
# «unhealthy» para siempre sin que nada este roto.
#
# Se instala antes de cambiar de usuario porque apt necesita root.
RUN apt-get update     && apt-get install -y --no-install-recommends curl     && rm -rf /var/lib/apt/lists/*

# Usuario sin privilegios. Las imágenes de .NET traen definido `app` (UID 64198)
# justamente para esto: si alguien logra ejecutar código en el contenedor,
# arranca sin ser root.
USER $APP_UID

# Kestrel escucha en todas las interfaces del contenedor. Dentro de un
# contenedor no hay nada más escuchando, y `localhost` lo dejaría inalcanzable
# desde afuera.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "PredictIT.Api.dll"]
